﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace disfr.UI
{
    public class GlossyTextBlock : FlowDocumentScrollViewer
    {
        static GlossyTextBlock()
        {
            InitializeGlossMap();
        }

        public GlossyTextBlock()
        {
            IsHitTestVisible = false;

            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            Paragraph = new Paragraph();
            Document = new FlowDocument(Paragraph);
            Document.PagePadding = new Thickness(0);
            Document.SetBinding(FlowDocument.FontFamilyProperty, new Binding() { Source = this, Path = new PropertyPath(FontFamilyProperty), Mode = BindingMode.OneWay });
            Document.SetBinding(FlowDocument.FontSizeProperty, new Binding() { Source = this, Path = new PropertyPath(FontSizeProperty), Mode = BindingMode.OneWay });
            Document.SetBinding(FlowDocument.TextAlignmentProperty, new Binding() { Source = this, Path = new PropertyPath(HorizontalContentAlignmentProperty), Mode = BindingMode.OneWay });
        }

        private readonly Paragraph Paragraph;

        public GlossyString GlossyText
        {
            get { return GetValue(GlossyTextProperty) as GlossyString; }
            set { SetValue(GlossyTextProperty, value); }
        }

        public static readonly DependencyProperty GlossyTextProperty =
            DependencyProperty.Register("GlossyText", typeof(GlossyString), typeof(GlossyTextBlock), new PropertyMetadata(GlossyString.Empty, OnGlossyTextChanged));

        private class GlossEntry
        {
            public Brush Foreground;
            public Brush Background;
            public TextDecorationCollection Decorations;

            private static TextDecorationCollection None = new TextDecorationCollection();

            public GlossEntry(Brush foreground, Brush background, TextDecorationCollection decorations)
            {
                Foreground = foreground;
                Background = background;
                Decorations = decorations ?? None;
            }
        }

        private static GlossEntry[] GlossMap;

        private static void InitializeGlossMap()
        {
            var map = new GlossEntry[Enum.GetValues(typeof(Gloss)).Cast<int>().Aggregate((x, y) => x | y) + 1];

            Brush NOR = Brushes.Black;
            Brush TAG = Brushes.Purple;
            Brush SYM = Brushes.Gray;

            Brush COMBG = Brushes.Transparent;
            Brush INSBG = Brushes.LightBlue;
            Brush DELBG = Brushes.LightPink;

            map[(int)(Gloss.NOR | Gloss.COM)] = new GlossEntry(NOR, COMBG, null);
            map[(int)(Gloss.NOR | Gloss.INS)] = new GlossEntry(NOR, INSBG, null);
            map[(int)(Gloss.NOR | Gloss.DEL)] = new GlossEntry(NOR, DELBG, null);
            map[(int)(Gloss.TAG | Gloss.COM)] = new GlossEntry(TAG, COMBG, null);
            map[(int)(Gloss.TAG | Gloss.INS)] = new GlossEntry(TAG, INSBG, null);
            map[(int)(Gloss.TAG | Gloss.DEL)] = new GlossEntry(TAG, DELBG, null);
            map[(int)(Gloss.SYM | Gloss.COM)] = new GlossEntry(SYM, COMBG, null);
            map[(int)(Gloss.SYM | Gloss.INS)] = new GlossEntry(SYM, INSBG, null);
            map[(int)(Gloss.SYM | Gloss.DEL)] = new GlossEntry(SYM, DELBG, null);

#if !DEBUG
            // Fill unused positions with default for safety.
            for (var i = 1; i < map.Length; i++)
            {
                if (map[i] == null) map[i] = map[(int)(Gloss.NOR | Gloss.COM)];
            }
#endif
            GlossMap = map;
        }

        public static void OnGlossyTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as GlossyTextBlock;
            var inlines = control.Paragraph.Inlines;
            inlines.Clear();
            var gs = e.NewValue as GlossyString;
            if (!GlossyString.IsNullOrEmpty(gs))
            {
                foreach (var p in gs.AsCollection())
                {
                    var entry = GlossMap[(int)p.Gloss];
                    inlines.Add(new Run(p.Text) { Foreground = entry.Foreground, Background = entry.Background, TextDecorations = entry.Decorations });
                }
            }
        }
    }
}
