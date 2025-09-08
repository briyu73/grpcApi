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

using DataGridGroupSortFilterUltimateExample.Auxilary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DataGridGroupSortFilterUltimateExample.ViewModels
{
    /// <summary>
    /// Container for a group description that lets the user toggle it on and off
    /// </summary>
    public class GroupDescriptionOption : NotifyPropertyChanged
    {
        private bool _isActive = false;

        public GroupDescriptionOption(bool isActive, string propertyName)
        {
            IsActive = isActive;
            PropertyName = propertyName;
        }

        public PropertyGroupDescription GroupDescription { get; } = new PropertyGroupDescription();

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string PropertyName
        {
            get => GroupDescription.PropertyName;
            private set => GroupDescription.PropertyName = value;
        }
    }
}
