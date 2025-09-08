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
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataGridGroupSortFilterUltimateExample.ViewModels
{
    /// <summary>
    /// ProjectDetails, and example class that is used for the items
    /// in the example collection.
    /// </summary>
    public class ProjectDetails : NotifyPropertyChanged
    {
        private string _projectName = string.Empty;
        private string _taskName = string.Empty;
        private DateTime _dueDate = DateTime.Now;
        private CompleteStatus _status = CompleteStatus.Active;

        private static int _itemCount = 0;

        public static ProjectDetails GetNewProject()
        {
            var i = Interlocked.Increment(ref _itemCount);
            return new ProjectDetails()
            {
                ProjectName = "Project " + ((i % 3) + 1).ToString(),
                TaskName = "Task " + i.ToString().PadLeft(2, '0'),
                DueDate = DateTime.Now.AddDays(i),
                Status = (i % 2 == 0) ? CompleteStatus.Complete : CompleteStatus.Active
            };
        }

        public override string ToString()
        {
            return $"{ProjectName} {TaskName} Due {DueDate} Status: {Status}";
        }

        // Public properties.
        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        public DateTime DueDate
        {
            get => _dueDate;
            set => SetProperty(ref _dueDate, value);
        }

        public CompleteStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

    }
}
