﻿/**
 * Editor for MARC records
 *
 * This project is built upon the CSharp_MARC project of the same name available
 * at http://csharpmarc.net, which itself is based on the File_MARC package
 * (http://pear.php.net/package/File_MARC) by Dan Scott, which was based on PHP
 * MARC package, originally called "php-marc", that is part of the Emilda
 * Project (http://www.emilda.org). Both projects were released under the LGPL
 * which allowed me to port the project to C# for use with the .NET Framework.
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 * @author    Matt Schraeder-Urbanowicz <matt@csharpmarc.net>
 * @copyright 2016-2017 Mattie Schraeder-Urbanowicz
 * @license   http://www.gnu.org/licenses/gpl-3.0.html  GPL License 3
 */

using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;

namespace CSharp_MARC_Editor
{
    public partial class ReportForm : Form
    {
        public DataTable DataTable { get; set; }

        public ReportDataSourceCollection DataSources => reportViewer.LocalReport.DataSources;

        public string Report
        {
            get { return reportViewer.LocalReport.ReportEmbeddedResource; }
            set { reportViewer.LocalReport.ReportEmbeddedResource = value; }
        }

        public ReportForm()
        {
            InitializeComponent();
        }

        private void ReportForm_Shown(object sender, EventArgs e)
        {
            reportViewer.RefreshReport();
        }
    }
}
