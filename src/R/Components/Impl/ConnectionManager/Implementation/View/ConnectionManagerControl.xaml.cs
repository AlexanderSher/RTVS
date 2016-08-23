﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Common.Core;
using Microsoft.R.Components.ConnectionManager.ViewModel;

namespace Microsoft.R.Components.ConnectionManager.Implementation.View {
    /// <summary>
    /// Interaction logic for ConnectionManagerControl.xaml
    /// </summary>
    public partial class ConnectionManagerControl : UserControl {

        private IConnectionManagerViewModel Model => DataContext as IConnectionManagerViewModel;

        public ConnectionManagerControl() {
            InitializeComponent();
        }


        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var connection = e.AddedItems.OfType<IConnectionViewModel>().FirstOrDefault();
            if (connection != null) {
                Model.SelectConnection(connection);
                List.ScrollIntoView(connection);
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e) {
            Model?.CancelSelected();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e) {
            Model?.SaveSelected();
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e) {
            Model?.ConnectAsync(GetConnection(e)).DoNotWait();
        }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e) {
            Model?.AddNew();
        }

        private static IConnectionViewModel GetConnection(RoutedEventArgs e) => ((IConnectionViewModel)((FrameworkElement)e.Source).DataContext);

    }
}
