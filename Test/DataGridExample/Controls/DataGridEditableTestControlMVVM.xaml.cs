// Classification: OFFICIAL
// 
// Copyright (C) 2021 Commonwealth of Australia.
// 
// All rights reserved.
// 
// The copyright herein resides with the Commonwealth of Australia.
// The material(s) may not be used, modified, copied and/or distributed
// without the written permission of the Commonwealth of Australia
// represented by Defence Science and Technology Group, the Department
// of Defence. The copyright notice above does not evidence any actual or 
// intended publication of such material(s).
// 
// This material is provided on an "AS IS" basis and the Commonwealth of
// Australia makes no representation or warranties of any kind, express 
// or implied, of merchantability or fitness for any purpose. The
// Commonwealth of Australia does not accept any liability arising from or
// connected to the use of the material.
// 
// Use of the material is entirely at the Licensee's own risk.

using DataGridGroupSortFilterUltimateExample.ViewModels;
using System.Windows.Controls;
using System.Windows.Data;

namespace DataGridGroupSortFilterUltimateExample.Controls
{
    /// <summary>
    /// Interaction logic for ConcurrentTestControl.xaml
    /// </summary>
    public partial class DataGridEditableTestControlMVVM : UserControl
    {
        public DataGridEditableTestControlMVVM()
        {
            DataContext = new DataGridConcurrentTestViewModel();
            InitializeComponent();
        }

        /// <summary>
        /// Handles when the Multi-Selected Items DataGrid adds columns to allow instant changing of
        /// value.s Don't do it with the main grid because there's an exception thrown due to a sort
        /// occuring during an edit.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.Column is DataGridComboBoxColumn comboBoxColumn)
            {
                if (comboBoxColumn.SelectedItemBinding is Binding binding)
                {
                    binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                }
            }
            else if (e.Column is DataGridBoundColumn boundColumn)
            {
                if (boundColumn.Binding is Binding binding)
                {
                    binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                }
            }
            
        }
    }
}
