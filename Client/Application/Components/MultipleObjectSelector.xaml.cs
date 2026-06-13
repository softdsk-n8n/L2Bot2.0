using Client.Domain.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Client.Application.Components
{
    /// <summary>
    /// Interaction logic for MobSelector.xaml
    /// </summary>
    public partial class MultipleObjectSelector : UserControl
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ICollection<ObjectInfo>), typeof(MultipleObjectSelector), new PropertyMetadata(default(ICollection<ObjectInfo>)));
        public ICollection<ObjectInfo> Source
        {
            get { return (ICollection<ObjectInfo>)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(ICollection<ObjectInfo>), typeof(MultipleObjectSelector), new PropertyMetadata(default(ICollection<ObjectInfo>)));
        public ICollection<ObjectInfo> Target
        {
            get { return (ICollection<ObjectInfo>)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }
        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(MultipleObjectSelector), new PropertyMetadata(""));

        public static RoutedUICommand AddItemCommand { get; } = new RoutedUICommand(
            "Add item",
            nameof(AddItemCommand),
            typeof(MultipleObjectSelector)
        );
        public static RoutedUICommand RemoveItemCommand { get; } = new RoutedUICommand(
            "Remove item",
            nameof(RemoveItemCommand),
            typeof(MultipleObjectSelector)
        );

        public MultipleObjectSelector()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(AddItemCommand, AddItem));
            CommandBindings.Add(new CommandBinding(RemoveItemCommand, RemoveItem));
        }

        private void AddItem(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var item = e.Parameter as ObjectInfo;
                if (item != null)
                {
                    Source.Remove(item);
                    Target.Add(item);
                    RefreshViews();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void RemoveItem(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var item = e.Parameter as ObjectInfo;
                if (item != null)
                {
                    Target.Remove(item);
                    Source.Add(item);
                    RefreshViews();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void sourceSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var view = Resources["sourceView"] as CollectionViewSource;
                if (view?.View != null)
                {
                    view.View.Refresh();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void targetSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var view = Resources["targetView"] as CollectionViewSource;
                if (view?.View != null)
                {
                    view.View.Refresh();
                }
            }
            catch
            {
                // ignore
            }
        }

        private string SourceFilterText
        {
            get { return sourceSearch?.Text ?? ""; }
        }

        private string TargetFilterText
        {
            get { return targetSearch?.Text ?? ""; }
        }

        private void SourceView_Filter(object sender, FilterEventArgs e)
        {
            try
            {
                var text = SourceFilterText;
                if (string.IsNullOrEmpty(text))
                {
                    e.Accepted = true;
                    return;
                }

                if (e.Item is ObjectInfo info && info.Name != null)
                {
                    e.Accepted = info.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    e.Accepted = false;
                }
            }
            catch
            {
                e.Accepted = true;
            }
        }

        private void TargetView_Filter(object sender, FilterEventArgs e)
        {
            try
            {
                var text = TargetFilterText;
                if (string.IsNullOrEmpty(text))
                {
                    e.Accepted = true;
                    return;
                }

                if (e.Item is ObjectInfo info && info.Name != null)
                {
                    e.Accepted = info.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    e.Accepted = false;
                }
            }
            catch
            {
                e.Accepted = true;
            }
        }

        private void RefreshViews()
        {
            try
            {
                var srcView = Resources["sourceView"] as CollectionViewSource;
                srcView?.View?.Refresh();

                var tgtView = Resources["targetView"] as CollectionViewSource;
                tgtView?.View?.Refresh();
            }
            catch
            {
                // ignore
            }
        }
    }
}
