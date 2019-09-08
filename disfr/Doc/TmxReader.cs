﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace disfr.Doc
{
    public class TmxReader : IAssetReader
    {
        private static readonly string[] _FilterString = { "TMX Translation Memory|*.tmx" };

        public IList<string> FilterString { get { return _FilterString; } }

        public string Name { get { return "TmxReader"; } }

        public int Priority { get { return 7; } }

        public IEnumerable<IAsset> Read(string filename, int filterindex)
        {
            using (var stream = File.OpenRead(filename))
            {
                return Read(stream, filename);
            }
        }

        private static readonly XNamespace TMX = XNamespace.Get("http://www.lisa.org/tmx14");

        private static readonly XName XML_LANG = XNamespace.Xml + "lang";

        private static readonly XName LANG = "lang";

        private class PairStore
        {
            private readonly object Lock = new object();

            private readonly Dictionary<string, List<ITransPair>> Store = new Dictionary<string, List<ITransPair>>(StringComparer.OrdinalIgnoreCase);

            public void Add(int index, string tlang, ITransPair pair)
            {
                lock (Lock)
                {
                    List<ITransPair> list;
                    if (!Store.TryGetValue(tlang, out list))
                    {
                        list = new List<ITransPair>();
                        Store.Add(tlang, list);
                    }
                    list.Add(pair);
                }
            }

            private IEnumerable<string> TargetLanguages;


            public IEnumerable<string> GetTargetLanguages()
            {
                if (TargetLanguages != null) return TargetLanguages;

                var all_tlangs = Store.Keys.ToArray();
                var unique_tlangs = all_tlangs.Where(t => !all_tlangs.Any(v => v != t && Covers(v, t))).ToList();
                unique_tlangs.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var u in unique_tlangs)
                {
                    foreach (var v in all_tlangs)
                    {
                        if (u != v && Covers(u, v))
                        {
                            Store[u].AddRange(Store[v]);
                            Store.Remove(v);
                        }
                    }
                }

                foreach (var list in Store.Values)
                {
                    list.Sort((x, y) => Comparer<int>.Default.Compare(x.Serial, y.Serial));
                }

                TargetLanguages = unique_tlangs;
                return TargetLanguages;
            }

            public IEnumerable<ITransPair> GetPairs(string tlang)
            {
                return Store[tlang];
            }
        }

        private struct SegPlus
        {
            public XElement Seg;
            public List<KeyValuePair<string, string>> Props;
            public List<string> Notes;
        }

        /// <summary>
        /// Task-local work variables to be used in <see cref="Parallel.For{TLocal}(int, int, Func{TLocal}, Func{int, ParallelLoopState, TLocal, TLocal}, Action{TLocal})"/>.
        /// </summary>
        private class Locals
        {
            public readonly Dictionary<string, SegPlus> TSegs;

            public readonly List<KeyValuePair<string, string>> TuProps;
            public readonly List<string> TuNotes;

            public readonly Dictionary<InlineTag, int> TagPool;

            public Locals()
            {
                TSegs = new Dictionary<string, SegPlus>();

                TuProps = new List<KeyValuePair<string, string>>();
                TuNotes = new List<string>();

                TagPool = new Dictionary<InlineTag, int>();
            }
        }


        public IEnumerable<IAsset> Read(Stream stream, string package)
        {
            XElement tmx = stream.PeekElementWithoutChildren();

            var X = tmx.Name.Namespace;
            if (tmx.Name.LocalName != "tmx" || (X != TMX && X != XNamespace.None))
            {
                return null;
            }

            try
            {
                tmx = XElement.Load(stream, LoadOptions.PreserveWhitespace);
            }
            catch (Exception)
            {
                return null;
            }

            var tus = tmx.Element(X + "body").Elements(X + "tu");
            var pool = new ConcurrentStringPool();

            var slang = DetectSourceLanguage(tmx);
            if (slang == null) return null;

            var propman = new PropertiesManager(true);
            var pairs = new PairStore();
            var locals_pool = new ConcurrentStack<Locals>();
            Parallel.ForEach(tus,
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                // Local Init
                () =>
                {
                    Locals locals;
                    return locals_pool.TryPop(out locals) ? locals : new Locals();
                },
                // Body
                (tu, state, index_long, locals) =>
                {
                    var index = (int)index_long;
                    var tsegs = locals.TSegs;
                    var tu_props = locals.TuProps;
                    var tu_notes = locals.TuNotes;
                    var tag_pool = locals.TagPool;

                    var sseg = new SegPlus();
                    tsegs.Clear();

                    CollectProps(ref tu_props, tu);
                    CollectNotes(ref tu_notes, tu);

                    foreach (var tuv in tu.Elements(X + "tuv"))
                    {
                        var lang = Lang(tuv);
                        if (Covers(slang, lang))
                        {
                            sseg.Seg = tuv.Element(X + "seg");
                            CollectProps(ref sseg.Props, tuv);
                            CollectNotes(ref sseg.Notes, tuv);
                        }
                        else
                        {
                            var tseg = new SegPlus();
                            tseg.Seg = tuv.Element(X + "seg");
                            CollectProps(ref tseg.Props, tuv);
                            CollectNotes(ref tseg.Notes, tuv);
                            tsegs[lang] = tseg;
                        }
                    }

                    if (sseg.Seg != null)
                    {
                        var id = (string)tu.Attribute("tuid") ?? "";
                        var source = NumberTags(tag_pool, GetInline(sseg.Seg, X));
                        var source_lang = Lang(sseg.Seg.Parent);

                        foreach (var kvp in tsegs)
                        {
                            var target_lang = kvp.Key;
                            var target_seg = kvp.Value.Seg;
                            var target_props = kvp.Value.Props;
                            var target_notes = kvp.Value.Notes;

                            if (target_seg != null)
                            {
                                var pair = new TmxPair()
                                {
                                    Serial = index + 1,
                                    Id = id,
                                    Source = source,
                                    Target = MatchTags(tag_pool, GetInline(target_seg, X)),
                                    SourceLang = source_lang,
                                    TargetLang = target_lang,
                                };
                                SetProps(propman, pair, tu_props, pool);
                                SetProps(propman, pair, sseg.Props, pool);
                                SetProps(propman, pair, target_props, pool);
                                pair.AddNotes(tu_notes.Concat(sseg.Notes).Concat(target_notes));
                                pairs.Add(index, target_lang, pair);
                            }
                        }
                    }
                    return locals;
                },
                // Local Finally
                locals =>
                {
                    locals_pool.Push(locals);
                }
            );
            locals_pool.Clear();

            var assets = new List<IAsset>();
            foreach (var tlang in pairs.GetTargetLanguages())
            {
                var asset = new TmxAsset()
                {
                    Package = package,
                    Original = string.Format("{0} - {1}", slang, tlang),
                    SourceLang = slang,
                    TargetLang = tlang,
                    TransPairs = pairs.GetPairs(tlang),
                    Properties = propman.Properties,
                };
                assets.Add(asset);
            }
            return assets;
        }

        /// <summary>
        /// Detect the source language and target languages. 
        /// </summary>
        /// <param name="tmx">The &lt;tmx&gt; element.</param>
        /// <returns>An array of language codes, whose element at [0] is the source language.</returns>
        /// <remarks>
        /// Language codes are defined being case insensitive.
        /// This method takes care of that feature.
        /// </remarks>
        private string DetectSourceLanguage(XElement tmx)
        {
            var X = tmx.Name.Namespace;

            var slang = (string)tmx.Element(X + "header").Attribute("srclang");
            if (string.Equals(slang, "*all*", StringComparison.OrdinalIgnoreCase)) return null;
            return slang;
        }

        /// <summary>
        /// Checks a language code covers another.
        /// </summary>
        /// <param name="parent">A language code that may cover <paramref name="code"/>.</param>
        /// <param name="code">A language code that may be covered by <paramref name="parent"/>.</param>
        /// <returns>
        /// True if <paramref name="parent"/> covers <paramref name="code"/>.  False otherwise.
        /// </returns>
        /// <remarks>
        /// A langauge code covers itself.
        /// A langauge code covers another langauge code if all subtags are included in the other.
        /// Cases are insignificant (and the casing is via so-called ordinal manner.
        /// </remarks>
        /// <example>
        /// "en" covers "en" itself, "en-GB", "en-US", and "en-US-VA", but it doesn't cover "fr".
        /// "en-US" covers "en-US-VA" but doesn't cover "en" or "en-GB".
        /// </example>
        private static bool Covers(string parent, string code)
        {
            if (parent.Length == code.Length)
            {
                return code.Equals(parent, StringComparison.OrdinalIgnoreCase);
            }
            else if (parent.Length < code.Length)
            {
                return code.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && code[parent.Length] == '-';
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get the language code of a tuv element.
        /// </summary>
        /// <param name="tuv">tuv element.</param>
        /// <returns>Language code as specified by xml:lang attribute or lang.</returns>
        private static string Lang(XElement tuv)
        {
            return (string)tuv.Attribute(XML_LANG) ?? (string)tuv.Attribute(LANG);
        }

        private static InlineString GetInline(XElement elem, XNamespace X)
        {
            var inline = new InlineString();
            BuildInline(inline, elem, X);
            return inline;
        }

        private static void BuildInline(InlineString inline, XElement elem, XNamespace X)
        {
            foreach (var node in elem.Nodes())
            {
                if (node is XText)
                {
                    inline.Add((node as XText).Value);
                }
                else if (node is XElement)
                {
                    var e = (XElement)node;
                    var ns = e.Name.Namespace;
                    var name = e.Name.LocalName;
                    if (ns == X && name == "bpt")
                    {
                        inline.Add(BuildNativeCodeTag(Tag.B, e, true));
                    }
                    else if (ns == X && name == "ept")
                    {
                        inline.Add(BuildNativeCodeTag(Tag.E, e, true));
                    }
                    else if (ns == X && name == "hi")
                    {
                        inline.Add(BuildNativeCodeTag(Tag.B, e, false));
                        BuildInline(inline, e, X);
                        inline.Add(BuildNativeCodeTag(Tag.E, e, false));
                    }
                    else if (ns == X && name == "it")
                    {
                        Tag type;
                        switch ((string)e.Attribute("pos"))
                        {
                            case "open": type = Tag.B; break;
                            case "close": type = Tag.E; break;
                            default: type = Tag.S; break;
                        }
                        inline.Add(BuildNativeCodeTag(type, e, true));
                    }
                    else if (ns == X && (name == "ph" || name == "ut"))
                    {
                        // Replace a standalone native code element with a standalone inline tag.
                        inline.Add(BuildNativeCodeTag(Tag.S, e, true));
                    }
                    else
                    {
                        // Uunknown element.
                        // OH, I have no good idea how to handle it.  FIXME.
                        if (string.IsNullOrEmpty((string)e) && !e.HasElements)
                        {
                            inline.Add(BuildNativeCodeTag(Tag.S, e, false));
                        }
                        else
                        {
                            inline.Add(BuildNativeCodeTag(Tag.B, e, false));
                            BuildInline(inline, e, X);
                            inline.Add(BuildNativeCodeTag(Tag.E, e, false));
                        }
                    }
                }
                else
                {
                    // Silently discard any other nodes; e.g., comment or pi. 
                }
            }
        }

        private static InlineTag BuildNativeCodeTag(Tag type, XElement elem, bool has_code)
        {
            return new InlineTag(
                type: type,
                id: (string)elem.Attribute("x") ?? "*",
                rid: (string)elem.Attribute("i") ?? "*",
                name: elem.Name.LocalName,
                ctype: (string)elem.Attribute("type"),
                display: null,
                code: has_code ? elem.Value : null);
        }

        private static InlineString NumberTags(Dictionary<InlineTag, int> pool, InlineString source)
        {
            pool.Clear();
            int n = 0;
            foreach (var tag in source.OfType<InlineTag>())
            {
                pool[tag] = tag.Number = ++n;
            }
            return source;
        }

        private static InlineString MatchTags(Dictionary<InlineTag, int> pool, InlineString target)
        {
            foreach (var tag in target.OfType<InlineTag>())
            {
                int m;
                pool.TryGetValue(tag, out m);
                tag.Number = m;
            }
            return target;
        }

        // <paramref name="elem"/> should either be TMX:tu or TMX:tuv.
        private static void CollectProps(ref List<KeyValuePair<string, string>> props, XElement elem)
        {
            var X = elem.Name.Namespace;
            if (props == null)
            {
                props = new List<KeyValuePair<string, string>>();
            }
            else
            {
                props.Clear();
            }
            props.AddRange(elem.Attributes().Where(a => a.Name.Namespace != XNamespace.Xml).Select(a => new KeyValuePair<string, string>(a.Name.LocalName, (string)a)));
            props.AddRange(elem.Elements(X + "prop").Select(p => new KeyValuePair<string, string>((string)p.Attribute("type"), (string)p)));
        }

        private static void CollectNotes(ref List<string> notes, XElement elem)
        {
            var X = elem.Name.Namespace;
            if (notes == null)
            {
                notes = new List<string>();
            }
            else
            {
                notes.Clear();
            }
            notes.AddRange(elem.Elements(X + "note").Select(n => (string)n));
        }


        private static void SetProps(PropertiesManager manager, TmxPair pair, IEnumerable<KeyValuePair<string, string>> props, IStringPool pool)
        {
            foreach (var kvp in props)
            {
                manager.Put(ref pair._Props, kvp.Key, pool.Intern(kvp.Value));
            }
        }
    }

    class TmxAsset : IAsset
    {
        public string Package { get; internal set; }

        public string Original { get; internal set; }

        public string SourceLang { get; internal set; }

        public string TargetLang { get; internal set; }

        public IEnumerable<ITransPair> TransPairs { get; internal set; }

        public IEnumerable<ITransPair> AltPairs { get { return Enumerable.Empty<ITransPair>(); } }

        public IList<PropInfo> Properties { get; internal set; }
    }

    class TmxPair : ITransPair
    {
        public int Serial { get; set; }

        public string Id { get; set; }

        public InlineString Source { get; set; }

        public InlineString Target { get; set; }

        public string SourceLang { get; set; }

        public string TargetLang { get; set; }

        private HashSet<string> _Notes = null;

        public IEnumerable<string> Notes { get { return _Notes; } }

        public void AddNotes(IEnumerable<string> notes) { (_Notes ?? (_Notes = new HashSet<string>())).UnionWith(notes); }

        internal string[] _Props = null;

        public string this[int key]
        {
            get
            {
                return (key < _Props?.Length) ? _Props[key] : null;
            }
        }
    }
}
