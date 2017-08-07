﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using MaterialControls;
using Naxam.Controls.Forms;
using Naxam.Controls.Platform.iOS;
using Naxam.Controls.Platform.iOS.Utils;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Platform.iOS;

[assembly: ExportRenderer(typeof(TopTabbedPage), typeof(TopTabbedRenderer))]

namespace Naxam.Controls.Platform.iOS
{
    using Platform = Xamarin.Forms.Platform.iOS.Platform;
    using Forms = Xamarin.Forms.Forms;

    public partial class TopTabbedRenderer :
        UIViewController
    {
        public static void Init()
        {
        }

        bool _barTextColorWasSet;
        UIColor _defaultBarColor;
        bool _defaultBarColorSet;
        bool _loaded;
        Size _queuedSize;

        Page Page => Element as Page;

        UIPageViewController pageViewController;

        protected UIViewController SelectedViewController;
        protected IList<UIViewController> ViewControllers;
        protected int SelectedIndex;

        protected IPageController PageController
        {
            get { return Page; }
        }

        protected TabbedPage Tabbed
        {
            get { return (TabbedPage) Element; }
        }

        protected TabsView TabBar;

        public TopTabbedRenderer()
        {
            ViewControllers = new UIViewController[0];

            pageViewController = new UIPageViewController(
                UIPageViewControllerTransitionStyle.Scroll,
                UIPageViewControllerNavigationOrientation.Horizontal,
                UIPageViewControllerSpineLocation.None
            );

            TabBar = new TabsView
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
        }

        public override void DidRotate(UIInterfaceOrientation fromInterfaceOrientation)
        {
            base.DidRotate(fromInterfaceOrientation);

            View.SetNeedsLayout();
        }

        public override void ViewDidAppear(bool animated)
        {
            PageController.SendAppearing();
            base.ViewDidAppear(animated);
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            PageController.SendDisappearing();
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();

            if (Element == null)
                return;

            if (!Element.Bounds.IsEmpty)
            {
                View.Frame = new System.Drawing.RectangleF((float) Element.X, (float) Element.Y, (float) Element.Width,
                    (float) Element.Height);
            }

            var frame = View.Frame;
            var tabBarFrame = TabBar.Frame;
            var y = tabBarFrame.Y + tabBarFrame.Height;
            PageController.ContainerArea = new Rectangle(0, 0, frame.Width, frame.Height);

            if (!_queuedSize.IsZero)
            {
                Element.Layout(new Rectangle(Element.X, Element.Y, _queuedSize.Width, _queuedSize.Height));
                _queuedSize = Size.Zero;
            }

            _loaded = true;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();


            View.AddSubview(TabBar);

            AddChildViewController(pageViewController);
            View.AddSubview(pageViewController.View);
            pageViewController.View.TranslatesAutoresizingMaskIntoConstraints = false;
            pageViewController.DidMoveToParentViewController(this);

            var layoutAttributes = new[]
            {
                NSLayoutAttribute.Leading,
                NSLayoutAttribute.Trailing
            };
            for (int i = 0; i < layoutAttributes.Length; i++)
            {
                View.AddConstraint(NSLayoutConstraint.Create(
                    TabBar,
                    layoutAttributes[i],
                    NSLayoutRelation.Equal,
                    View,
                    layoutAttributes[i],
                    1, 0
                ));
                View.AddConstraint(NSLayoutConstraint.Create(
                    pageViewController.View,
                    layoutAttributes[i],
                    NSLayoutRelation.Equal,
                    View,
                    layoutAttributes[i],
                    1, 0
                ));
            }

            View.AddConstraint(NSLayoutConstraint.Create(
                pageViewController.View,
                NSLayoutAttribute.Bottom,
                NSLayoutRelation.Equal,
                View,
                NSLayoutAttribute.Bottom,
                1, 0
            ));

            View.AddConstraint(NSLayoutConstraint.Create(
                TabBar,
                NSLayoutAttribute.Top,
                NSLayoutRelation.Equal,
                View,
                NSLayoutAttribute.Top,
                1, 0
            ));

            View.AddConstraint(NSLayoutConstraint.Create(
                pageViewController.View,
                NSLayoutAttribute.Top,
                NSLayoutRelation.Equal,
                TabBar,
                NSLayoutAttribute.Bottom,
                1, 0
            ));

            var tabBarHeight = ParentViewController != null
                ? 48
                : 68;
            TabBar.AddConstraint(NSLayoutConstraint.Create(
                TabBar,
                NSLayoutAttribute.Height,
                NSLayoutRelation.Equal,
                1, tabBarHeight
            ));

            pageViewController.SetViewControllers(
                new[] {ViewControllers[0]},
                UIPageViewControllerNavigationDirection.Forward,
                true, null
            );
            pageViewController.WeakDataSource = this;
            pageViewController.WeakDelegate = this;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PageController.SendDisappearing();
                Tabbed.PropertyChanged -= OnPropertyChanged;
                Tabbed.PagesChanged -= OnPagesChanged;
            }

            base.Dispose(disposing);
        }

        protected virtual void OnElementChanged(VisualElementChangedEventArgs e)
        {
            ElementChanged?.Invoke(this, e);
        }

        UIViewController GetViewController(Page page)
        {
            var renderer = Platform.GetRenderer(page);
            return renderer?.ViewController;
        }

        void OnPagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Setting TabBarItem.Title in iOS 10 causes rendering bugs
            // Work around this by creating a new UITabBarItem on each change
            if (e.PropertyName == Page.TitleProperty.PropertyName)
            {
                var page = (Page) sender;
                var renderer = Platform.GetRenderer(page);
                if (renderer == null)
                    return;

                if (renderer.ViewController.TabBarItem != null)
                    renderer.ViewController.TabBarItem.Title = page.Title;
            }
            else if (e.PropertyName == Page.IconProperty.PropertyName ||
                     e.PropertyName == Page.TitleProperty.PropertyName)
            {
                var page = (Page) sender;

                IVisualElementRenderer renderer = Platform.GetRenderer(page);

                if (renderer?.ViewController.TabBarItem == null)
                    return;
            }
        }

        void OnPagesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            e.Apply((o, i, c) => SetupPage((Page) o, i), (o, i) => TeardownPage((Page) o, i), Reset);

            SetControllers();

            UIViewController controller = null;
            if (Tabbed.CurrentPage != null)
                controller = GetViewController(Tabbed.CurrentPage);
            if (controller != null && controller != SelectedViewController)
                SelectedViewController = controller;
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabbedPage.CurrentPage))
            {
                var current = Tabbed.CurrentPage;
                if (current == null)
                    return;

                var controller = GetViewController(current);
                if (controller == null)
                    return;

                SelectedViewController = controller;
            }
            else if (e.PropertyName == TabbedPage.BarBackgroundColorProperty.PropertyName)
                UpdateBarBackgroundColor();
            else if (e.PropertyName == TabbedPage.BarTextColorProperty.PropertyName)
                UpdateBarTextColor();
        }

        public override UIViewController ChildViewControllerForStatusBarHidden()
        {
            var current = Tabbed.CurrentPage;
            if (current == null)
                return null;

            return GetViewController(current);
        }

        void Reset()
        {
            var i = 0;
            foreach (var page in Tabbed.Children)
            {
                SetupPage(page, i++);
            }
        }

        void SetControllers()
        {
            var list = new List<UIViewController>();
            var titles = new List<string>();
            for (var i = 0; i < Tabbed.Children.Count; i++)
            {
                var child = Tabbed.Children[i];
                var v = child as VisualElement;
                if (v == null)
                    continue;
                if (Platform.GetRenderer(v) != null)
                    list.Add(Platform.GetRenderer(v).ViewController);

                titles.Add(Tabbed.Children[i].Title);
            }
            ViewControllers = list.ToArray();
            TabBar.SetItems(titles);
        }

        void SetupPage(Page page, int index)
        {
            IVisualElementRenderer renderer = Platform.GetRenderer(page);
            if (renderer == null)
            {
                renderer = Platform.CreateRenderer(page);
                Platform.SetRenderer(page, renderer);
            }

            page.PropertyChanged += OnPagePropertyChanged;
        }

        void TeardownPage(Page page, int index)
        {
            page.PropertyChanged -= OnPagePropertyChanged;

            Platform.SetRenderer(page, null);
        }

        void UpdateBarBackgroundColor()
        {
            if (Tabbed == null || TabBar == null)
                return;

            var barBackgroundColor = Tabbed.BarBackgroundColor;

            if (!_defaultBarColorSet)
            {
                _defaultBarColor = TabBar.BackgroundColor;

                _defaultBarColorSet = true;
            }

            TabBar.BackgroundColor = barBackgroundColor.ToUIColor();
        }

        void UpdateBarTextColor()
        {
            TabBar.TextColor = Tabbed.BarTextColor.ToUIColor();
        }
    }
}