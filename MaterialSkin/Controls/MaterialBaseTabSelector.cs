﻿namespace MaterialSkin.Controls
{
    using MaterialSkin.Animations;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Drawing.Text;
    using System.Globalization;
    using System.Linq;
    using System.Windows.Forms;

    public class MaterialBaseTabSelector : Control, IMaterialControl
    {
        [Browsable(false)]
        public int Depth { get; set; }

        [Browsable(false)]
        public MaterialSkinManager SkinManager => MaterialSkinManager.Instance;

        [Browsable(false)]
        public MouseState MouseState { get; set; }

        //[Browsable(false)]
        public enum CustomCharacterCasing
        {
            [Description("Text will be used as user inserted, no alteration")]
            Normal,
            [Description("Text will be converted to UPPER case")]
            Upper,
            [Description("Text will be converted to lower case")]
            Lower,
            [Description("Text will be converted to Proper case (aka Title case)")]
            Proper
        }

        private TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

        private MaterialTabControl _baseTabControl;

        [Category("Appearance"), Localizable(true)]
        public override Font Font
        {
            get { return base.Font; }
            set
            {
                var font = new Font(SkinManager.GetFontFamily(SkinManager.CurrentFontFamily), value.SizeInPoints, value.Style, GraphicsUnit.Point);
                base.Font = font;
                Invalidate();
            }
        }

        [Category("Material Skin"), Browsable(true)]
        public MaterialTabControl BaseTabControl
        {
            get { return _baseTabControl; }
            set
            {
                _baseTabControl = value;
                if (_baseTabControl == null) return;

                UpdateTabRects();

                _previousSelectedTabIndex = _baseTabControl.SelectedIndex;
                _baseTabControl.Deselected += (sender, args) =>
                {
                    _previousSelectedTabIndex = _baseTabControl.SelectedIndex;
                };
                _baseTabControl.SelectedIndexChanged += (sender, args) =>
                {
                    _animationManager.SetProgress(0);
                    _animationManager.StartNewAnimation(AnimationDirection.In);
                };
                _baseTabControl.ControlAdded += delegate
                {
                    Invalidate();
                };
                _baseTabControl.ControlRemoved += delegate
                {
                    Invalidate();
                };
            }
        }

        [Category("Material Skin"), DefaultValue(160), Browsable(true)]
        public int TabWidthMin {
            get => tabWidthMin;
            set
            {
                tabWidthMin = value;
                Invalidate();
            }
        }
        [Category("Material Skin"), DefaultValue(264), Browsable(true)]
        public int TabWidthMax
        {
            get => tabWidthMax;
            set
            {
                tabWidthMax = value;
                Invalidate();
            }
        }

        [Category("Material Skin"), DefaultValue(24), Browsable(true)]
        public int TabHeaderPadding
        {
            get => tabHeaderPadding;
            set
            {
                tabHeaderPadding = value;
                Invalidate();
            }
        }

        [Category("Material Skin"), DefaultValue(50), Browsable(true)]
        public int FirstTabPadding
        {
            get => firstTabPadding;
            set
            {
                firstTabPadding = value;
                Invalidate();
            }
        }

        private int firstTabPadding = 50;
        private int tabHeaderPadding = 24;
        private int tabWidthMin = 160;
        private int tabWidthMax = 264;
        private int _previousSelectedTabIndex;

        private Size originalSize;
        private Point originalLocation;
        private bool isMouseWheelAction = false;

        private Point _animationSource;

        private readonly AnimationManager _animationManager;

        private List<Rectangle> _tabRects;

        private const int ICON_SIZE = 24;

        private int _tab_over_index = -1;

        private CustomCharacterCasing _characterCasing;

        public int FullWidth
        {
            get => _tabRects == null ? 0 : _tabRects.Sum(rect => rect.Width);
        }

        [Category("Appearance")]
        public CustomCharacterCasing CharacterCasing
        {
            get => _characterCasing;
            set
            {
                _characterCasing = value;
                _baseTabControl.Invalidate();
                Invalidate();
            }
        }
        private int _tab_indicator_height;

        [Category("Material Skin"), Browsable(true), DisplayName("Tab Indicator Height"), DefaultValue(2)]
        public int TabIndicatorHeight 
        {
            get { return _tab_indicator_height; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Tab Indicator Height", value, "Value should be > 0");
                else
                {
                    _tab_indicator_height = value;
                    Refresh();
                }
            }
        }

        public enum TabLabelStyle
        {
            Text,
            Icon,
            IconAndText,
        }

        private TabLabelStyle _tabLabel;
        [Category("Material Skin"), Browsable(true), DisplayName("Tab Label"), DefaultValue(TabLabelStyle.Text)]
        public TabLabelStyle TabLabel
        {
            get { return _tabLabel; }
            set
            {
                _tabLabel = value;
                if (_tabLabel == TabLabelStyle.IconAndText && Height < 24)
                    Height = 24;

                UpdateTabRects();
                Invalidate();
            }
        }


        public MaterialBaseTabSelector()
        {
            SetStyle(ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);
            Font = SkinManager.getFontByType(MaterialSkinManager.fontType.Body1);
            TabIndicatorHeight = 2;
            TabLabel = TabLabelStyle.Text;

            Size = new Size(480, 48);

            _animationManager = new AnimationManager
            {
                AnimationType = AnimationType.EaseOut,
                Increment = 0.04
            };
            _animationManager.OnAnimationProgress += sender => Invalidate();
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            g.Clear(SkinManager.ColorScheme.PrimaryColor);

            if (_baseTabControl == null) return;

            if (!_animationManager.IsAnimating() || _tabRects == null || _tabRects.Count != _baseTabControl.TabCount)
                UpdateTabRects();

            var animationProgress = _animationManager.GetProgress();

            //Click feedback
            if (_animationManager.IsAnimating())
            {
                var rippleBrush = new SolidBrush(Color.FromArgb((int)(51 - (animationProgress * 50)), Color.White));
                var rippleSize = (int)(animationProgress * _tabRects[_baseTabControl.SelectedIndex].Width * 1.75);

                g.SetClip(_tabRects[_baseTabControl.SelectedIndex]);
                g.FillEllipse(rippleBrush, new Rectangle(_animationSource.X - rippleSize / 2, _animationSource.Y - rippleSize / 2, rippleSize, rippleSize));
                g.ResetClip();
                rippleBrush.Dispose();
            }

            //Draw tab headers
            if (_tab_over_index >= 0)
            { 
                //Change mouse over tab background color
                g.FillRectangle(SkinManager.BackgroundHoverBrush , _tabRects[_tab_over_index].X, _tabRects[_tab_over_index].Y , _tabRects[_tab_over_index].Width, _tabRects[_tab_over_index].Height - _tab_indicator_height);
            }

            foreach (TabPage tabPage in _baseTabControl.TabPages)
            {
                var currentTabIndex = _baseTabControl.TabPages.IndexOf(tabPage);

                if (_tabLabel != TabLabelStyle.Icon)
                {
                    // Text
                    using (NativeTextRenderer NativeText = new NativeTextRenderer(g))
                    {
                        Size textSize = TextRenderer.MeasureText(_baseTabControl.TabPages[currentTabIndex].Text, Font);
                        Rectangle textLocation = new Rectangle(
                            _tabRects[currentTabIndex].X + (tabHeaderPadding / 2),
                            0,
                            _tabRects[currentTabIndex].Width - (tabHeaderPadding),
                            Height);

                        if (_tabLabel == TabLabelStyle.IconAndText)
                        {
                            //textLocation.Y = 46;
                            //textLocation.Height = 26;
                        }

                        if (((tabHeaderPadding * 2) + textSize.Width < TabWidthMax))
                        {
                            NativeText.DrawTransparentText(
                            CharacterCasing == CustomCharacterCasing.Upper ? tabPage.Text.ToUpper() :
                            CharacterCasing == CustomCharacterCasing.Lower ? tabPage.Text.ToLower() :
                            CharacterCasing == CustomCharacterCasing.Proper ? textInfo.ToTitleCase(tabPage.Text.ToLower()) : tabPage.Text,
                            Font,
                            Color.FromArgb(CalculateTextAlpha(currentTabIndex, animationProgress), SkinManager.ColorScheme.TextColor),
                            textLocation.Location,
                            textLocation.Size,
                            NativeTextRenderer.TextAlignFlags.Center | NativeTextRenderer.TextAlignFlags.Middle);
                        }
                        else
                        {
                            if (_tabLabel == TabLabelStyle.IconAndText)
                            {
                                //textLocation.Y = 40;
                                //textLocation.Height = 26;
                            }
                            NativeText.DrawMultilineTransparentText(
                            CharacterCasing == CustomCharacterCasing.Upper ? tabPage.Text.ToUpper() :
                            CharacterCasing == CustomCharacterCasing.Lower ? tabPage.Text.ToLower() :
                            CharacterCasing == CustomCharacterCasing.Proper ? textInfo.ToTitleCase(tabPage.Text.ToLower()) : tabPage.Text,
                            SkinManager.getFontByType(MaterialSkinManager.fontType.Body2),
                            Color.FromArgb(CalculateTextAlpha(currentTabIndex, animationProgress), SkinManager.ColorScheme.TextColor),
                            textLocation.Location,
                            textLocation.Size,
                            NativeTextRenderer.TextAlignFlags.Center | NativeTextRenderer.TextAlignFlags.Middle);
                        }
                    }
                }

                if (_tabLabel != TabLabelStyle.Text)
                {
                    // Icons
                    if (_baseTabControl.ImageList != null && (!String.IsNullOrEmpty(tabPage.ImageKey) | tabPage.ImageIndex > -1))
                    {
                        Rectangle iconRect = new Rectangle(
                            _tabRects[currentTabIndex].X + (_tabRects[currentTabIndex].Width / 2) - (ICON_SIZE / 2),
                            _tabRects[currentTabIndex].Y + (_tabRects[currentTabIndex].Height / 2) - (ICON_SIZE / 2),
                            ICON_SIZE, ICON_SIZE);
                        if (_tabLabel == TabLabelStyle.IconAndText)
                        {
                            iconRect.Y = 12;
                        }
                        g.DrawImage(!String.IsNullOrEmpty(tabPage.ImageKey) ? _baseTabControl.ImageList.Images[tabPage.ImageKey]: _baseTabControl.ImageList.Images[tabPage.ImageIndex], iconRect);
                    }
                }
           }

            //Animate tab indicator
            var previousSelectedTabIndexIfHasOne = _previousSelectedTabIndex == -1 ? _baseTabControl.SelectedIndex : _previousSelectedTabIndex;
            var previousActiveTabRect = _tabRects[previousSelectedTabIndexIfHasOne];
            var activeTabPageRect = _tabRects[_baseTabControl.SelectedIndex];

            var y = activeTabPageRect.Bottom - _tab_indicator_height;
            var x = previousActiveTabRect.X + (int)((activeTabPageRect.X - previousActiveTabRect.X) * animationProgress);
            var width = previousActiveTabRect.Width + (int)((activeTabPageRect.Width - previousActiveTabRect.Width) * animationProgress);

            g.FillRectangle(SkinManager.ColorScheme.AccentBrush, x, y, width, _tab_indicator_height);
        }

        private int CalculateTextAlpha(int tabIndex, double animationProgress)
        {
            int primaryA = SkinManager.TextHighEmphasisColor.A;
            int secondaryA = SkinManager.TextMediumEmphasisColor.A;

            if (tabIndex == _baseTabControl.SelectedIndex && !_animationManager.IsAnimating())
            {
                return primaryA;
            }
            if (tabIndex != _previousSelectedTabIndex && tabIndex != _baseTabControl.SelectedIndex)
            {
                return secondaryA;
            }
            if (tabIndex == _previousSelectedTabIndex)
            {
                return primaryA - (int)((primaryA - secondaryA) * animationProgress);
            }
            return secondaryA + (int)((primaryA - secondaryA) * animationProgress);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            
            originalSize = Size;
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            
            if (!isMouseWheelAction)
            {
                originalLocation = Location;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            isMouseWheelAction = true;
            var fullWidth = FirstTabPadding + FullWidth;
            int step = 20;
            
            if (e.Delta > 0)
            {
                Location = new Point(Location.X + step > 0 ? 0 : Location.X + step, Location.Y);
                Width = Location.X + step > 0 ? Parent.Width : Width - step;
            }
            else
            {
                if (Width < fullWidth)
                {
                    Location = new Point(Location.X - step, Location.Y);
                    Width += step;
                }
            }

            isMouseWheelAction = false;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_tabRects == null) UpdateTabRects();
            for (var i = 0; i < _tabRects.Count; i++)
            {
                if (_tabRects[i].Contains(e.Location))
                {
                    _baseTabControl.SelectedIndex = i;
                }
            }

            _animationSource = e.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (DesignMode)
                return;

            if (_tabRects == null)
                UpdateTabRects();

            int old_tab_over_index = _tab_over_index;
            _tab_over_index = -1;
            for (var i = 0; i < _tabRects.Count; i++)
            {
                if (_tabRects[i].Contains(e.Location))
                {
                    Cursor = Cursors.Hand;
                    _tab_over_index = i;
                    break;
                }
            }
            if (_tab_over_index == -1)
                Cursor = Cursors.Arrow;
            if (old_tab_over_index != _tab_over_index)
                Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (DesignMode)
                return;

            if (_tabRects == null)
                UpdateTabRects();

            Cursor = Cursors.Arrow;
            _tab_over_index = -1;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            UpdateTabRects();

            Height = Height < 24 ? 24 : Height;
            Size = new Size(Width, Height);
            Invalidate();
        }

        public void UpdateTabRects()
        {
            _tabRects = new List<Rectangle>();

            //If there isn't a base tab control, the rects shouldn't be calculated
            //If there aren't tab pages in the base tab control, the list should just be empty which has been set already; exit the void
            if (_baseTabControl == null || _baseTabControl.TabCount == 0) return;

            //Calculate the bounds of each tab header specified in the base tab control
            using (var b = new Bitmap(1, 1))
            {
                using (var g = Graphics.FromImage(b))
                {
                    using (NativeTextRenderer NativeText = new NativeTextRenderer(g))
                    {
                        for (int i = 0; i < _baseTabControl.TabPages.Count; i++)
                        {
                            Size textSize = TextRenderer.MeasureText(_baseTabControl.TabPages[i].Text, Font);
                            if (_tabLabel == TabLabelStyle.Icon) textSize.Width = ICON_SIZE;

                            int TabWidth = (tabHeaderPadding * 2) + textSize.Width;
                            if (TabWidth > TabWidthMax)
                                TabWidth = TabWidthMax;
                            else if (TabWidth < TabWidthMin)
                                TabWidth = TabWidthMin;

                            if (i==0)
                                _tabRects.Add(new Rectangle(FirstTabPadding - (TabHeaderPadding), 0, TabWidth, Height));
                            else
                                _tabRects.Add(new Rectangle(_tabRects[i - 1].Right, 0, TabWidth, Height));
                        }
                    }
                }
            }
        }
    }
}
