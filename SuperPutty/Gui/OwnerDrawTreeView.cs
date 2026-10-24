using System;
using System.Drawing;
using System.Windows.Forms;
using SuperPutty.Utils;

namespace SuperPutty.Gui
{
    /// <summary>What a pane draws into one row of an <see cref="OwnerDrawTreeView"/></summary>
    public class TreeRowDrawEventArgs : EventArgs
    {
        public Graphics Graphics { get; internal set; }
        public TreeNode Node { get; internal set; }
        /// <summary>The whole row, full width</summary>
        public Rectangle RowBounds { get; internal set; }
        /// <summary>The row minus indentation and chevron: where the pane's content goes</summary>
        public Rectangle Bounds { get; internal set; }
        public bool Selected { get; internal set; }
        /// <summary>The mouse is over the row</summary>
        public bool Hot { get; internal set; }
        /// <summary>Main text colour for this row's background</summary>
        public Color ForeColor { get; internal set; }
        /// <summary>Colour for secondary text (second line, counts)</summary>
        public Color SubtleColor { get; internal set; }
    }

    /// <summary>
    /// A TreeView whose rows the pane draws itself (DrawRow): rows of different heights (TVIF_INTEGRAL, in
    /// multiples of ItemHeight, see RowUnits), full-row selection and hover highlight, a chevron for nodes with
    /// children (click it, double-click the row or use the arrow keys to expand/collapse), no horizontal scroll
    /// bar, double-buffered. Keyboard navigation, tooltips and context menus stay the TreeView's own.
    /// </summary>
    public class OwnerDrawTreeView : TreeView
    {
        /// <summary>Indentation per level, in pixels</summary>
        public const int IndentWidth = 14;

        /// <summary>Room for the chevron in front of a node with children</summary>
        public const int ChevronWidth = 14;

        private TreeNode hotNode;
        private Font boldFont;
        private Font smallFont;
        private Font monoFont;

        /// <summary>Height of a node in multiples of ItemHeight (default 2)</summary>
        public Func<TreeNode, int> RowUnits { get; set; }

        /// <summary>Draws the content of a row; background, chevron and indentation are already drawn</summary>
        public event EventHandler<TreeRowDrawEventArgs> DrawRow;

        public OwnerDrawTreeView()
        {
            this.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            this.ShowLines = false;
            this.ShowPlusMinus = false;
            this.ShowRootLines = false;
            this.FullRowSelect = true;
            this.HideSelection = false;
            this.BorderStyle = BorderStyle.None;
            this.ItemHeight = 12;
        }

        public Font BoldFont
        {
            get { return this.boldFont ?? (this.boldFont = new Font(this.Font, FontStyle.Bold)); }
        }

        public Font SmallFont
        {
            get { return this.smallFont ?? (this.smallFont = new Font(this.Font.FontFamily, this.Font.Size - 1f)); }
        }

        /// <summary>Monospace font for commands, same size as the tree's font</summary>
        public Font MonoFont
        {
            get { return this.monoFont ?? (this.monoFont = new Font("Consolas", this.Font.Size)); }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= NativeMethods.TVS_NOHSCROLL;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SendMessage(this.Handle, NativeMethods.TVM_SETEXTENDEDSTYLE, (long)NativeMethods.TVS_EX_DOUBLEBUFFER, (long)NativeMethods.TVS_EX_DOUBLEBUFFER);
            // a new handle means newly inserted items without their heights
            ApplyRowHeights();
        }

        /// <summary>Set every node's height from RowUnits; call after filling the tree</summary>
        public void ApplyRowHeights()
        {
            if (!this.IsHandleCreated)
            {
                return; // OnHandleCreated applies them
            }
            // the tree lays out its rows again when redraw is switched back on; heights changed while it draws
            // only move the changed rows, not the ones below them
            BeginUpdate();
            try
            {
                ApplyRowHeights(this.Nodes);
            }
            finally
            {
                EndUpdate();
            }
        }

        private void ApplyRowHeights(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                NativeMethods.TVITEMEX item = new NativeMethods.TVITEMEX
                {
                    mask = NativeMethods.TVIF_HANDLE | NativeMethods.TVIF_INTEGRAL,
                    hItem = node.Handle,
                    iIntegral = Math.Max(1, this.RowUnits == null ? 2 : this.RowUnits(node))
                };
                NativeMethods.SendMessage(this.Handle, NativeMethods.TVM_SETITEMW, IntPtr.Zero, ref item);
                ApplyRowHeights(node.Nodes);
            }
        }

        protected override void OnDrawNode(DrawTreeNodeEventArgs e)
        {
            e.DrawDefault = false;
            if (e.Node == null || e.Bounds.Height <= 0 || e.Bounds.Width <= 0)
            {
                return; // the TreeView asks for hidden nodes too
            }
            Graphics g = e.Graphics;
            Rectangle row = new Rectangle(0, e.Bounds.Top, this.ClientSize.Width, e.Bounds.Height);
            bool selected = e.Node.IsSelected;
            bool hot = e.Node == this.hotNode;

            using (SolidBrush back = new SolidBrush(PaneStyle.RowBackground(this.BackColor, selected, this.Focused, hot)))
            {
                g.FillRectangle(back, row);
            }
            Color fore = PaneStyle.RowForeground(this.ForeColor, selected, this.Focused);
            Color subtle = PaneStyle.RowSubtle(selected, this.Focused);

            int x = e.Node.Level * IndentWidth;
            Rectangle content;
            if (e.Node.Nodes.Count > 0)
            {
                PaneStyle.DrawChevron(g, new Rectangle(x + 3, row.Top, ChevronWidth - 4, row.Height), e.Node.IsExpanded, subtle);
                content = Rectangle.FromLTRB(x + ChevronWidth + 2, row.Top, row.Right - 4, row.Bottom);
            }
            else
            {
                content = Rectangle.FromLTRB(x + 2, row.Top, row.Right - 4, row.Bottom);
            }

            EventHandler<TreeRowDrawEventArgs> handler = this.DrawRow;
            if (handler != null)
            {
                handler(this, new TreeRowDrawEventArgs
                {
                    Graphics = g,
                    Node = e.Node,
                    RowBounds = row,
                    Bounds = content,
                    Selected = selected,
                    Hot = hot,
                    ForeColor = fore,
                    SubtleColor = subtle
                });
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            TreeNode node = GetNodeAt(e.Location);
            if (node != this.hotNode)
            {
                this.hotNode = node;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (this.hotNode != null)
            {
                this.hotNode = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // right-click selects the row, so the context menu works on it
            if (e.Button == MouseButtons.Right)
            {
                TreeNode node = GetNodeAt(e.Location);
                if (node != null)
                {
                    this.SelectedNode = node;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnNodeMouseClick(TreeNodeMouseClickEventArgs e)
        {
            int x = e.Node.Level * IndentWidth;
            if (e.Button == MouseButtons.Left && e.Node.Nodes.Count > 0 && e.X >= x && e.X < x + ChevronWidth + 2)
            {
                e.Node.Toggle();
            }
            base.OnNodeMouseClick(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate(); // right-aligned content moves
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            DisposeFonts();
            base.OnFontChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeFonts();
            }
            base.Dispose(disposing);
        }

        private void DisposeFonts()
        {
            foreach (Font font in new[] { this.boldFont, this.smallFont, this.monoFont })
            {
                if (font != null)
                {
                    font.Dispose();
                }
            }
            this.boldFont = this.smallFont = this.monoFont = null;
        }
    }
}
