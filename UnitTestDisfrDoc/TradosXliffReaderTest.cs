﻿using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using disfr.Doc;

namespace UnitTestDisfrDoc
{
    [TestClass]
    public class TradosXliffReaderTest : ReaderTestBase
    {
        [TestMethod]
        public void ChangeTracking_1()
        {
            var path = Path.Combine(IDIR, "ChangeTracking_Trados_1.sdlxliff");
            var bundle = new XliffReader().Read(path);
            bundle.Assets.Count().Is(1);
            var pairs = bundle.Assets.ElementAt(0).TransPairs.ToArray();
            {
                pairs[0].Source.ToString().Is("Change Tracking");
                pairs[1].Source.ToString().Is("Paragraph #1.");
                pairs[2].Source.ToString().Is("Paragraph #2.");
                pairs[3].Source.ToString().Is("<b>Paragraph</b> #3 with <i>tags</i>.");
                pairs[4].Source.ToString().Is("<b>Paragraph</b> #4 with <i>tags</i>.");

                pairs[0].Source.ToString(InlineToString.Debug).Is("Change Tracking");
                pairs[1].Source.ToString(InlineToString.Debug).Is("Paragraph #1.");
                pairs[2].Source.ToString(InlineToString.Debug).Is("Paragraph #2.");
                pairs[3].Source.ToString(InlineToString.Debug).Is("{g;2}Paragraph{g;2} #3 with {g;5}tags{g;5}.");
                pairs[4].Source.ToString(InlineToString.Debug).Is("{g;8}Paragraph{g;8} #4 with {g;11}tags{g;11}.");

                pairs[0].Target.ToString().Is("CHANGE TRACKING");
                pairs[1].Target.ToString().Is("#1.");
                pairs[2].Target.ToString().Is("PARAGRAPH #2 INSERTED.");
                pairs[3].Target.ToString().Is("WITH <i>TAGS</i>, <b>PARAGRAPH</b> #3.");
                pairs[4].Target.ToString().Is("<b>ABRACADABRA</b> #4 with <i>TAGS</i>.");

                pairs[0].Target.ToString(InlineToString.Normal).Is("CHANGE TRACKING");
                pairs[1].Target.ToString(InlineToString.Normal).Is("#1.");
                pairs[2].Target.ToString(InlineToString.Normal).Is("PARAGRAPH #2 INSERTED.");
                pairs[3].Target.ToString(InlineToString.Normal).Is("WITH <i>TAGS</i>, <b>PARAGRAPH</b> #3.");
                pairs[4].Target.ToString(InlineToString.Normal).Is("<b>ABRACADABRA</b> #4 with <i>TAGS</i>.");

                pairs[0].Target.ToString(InlineToString.Flat).Is("CHANGE TRACKING");
                pairs[1].Target.ToString(InlineToString.Flat).Is("#1.");
                pairs[2].Target.ToString(InlineToString.Flat).Is("PARAGRAPH #2 INSERTED.");
                pairs[3].Target.ToString(InlineToString.Flat).Is("WITH TAGS, PARAGRAPH #3.");
                pairs[4].Target.ToString(InlineToString.Flat).Is("ABRACADABRA #4 with TAGS.");

                pairs[0].Target.ToString(InlineToString.Debug).Is("CHANGE TRACKING");
                pairs[1].Target.ToString(InlineToString.Debug).Is("{Del}PARAGRAPH {None}#1.");
                pairs[2].Target.ToString(InlineToString.Debug).Is("PARAGRAPH #2{Ins} INSERTED{None}.");
                pairs[3].Target.ToString(InlineToString.Debug).Is("{Del}{g;2}PARAGRAPH{g;2} #3 {None}WITH {g;5}TAGS{g;5}{Ins}, {g;2}PARAGRAPH{g;2} #3{None}.");
                pairs[4].Target.ToString(InlineToString.Debug).Is("{g;8}{Del}PARAGRAPH{Ins}ABRACADABRA{None}{g;8} #4 with {g;11}TAGS{g;11}.");
            }
        }
    }
}
