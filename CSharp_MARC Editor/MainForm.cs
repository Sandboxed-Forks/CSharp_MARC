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
 * @author    Matt Schraeder <mschraeder@csharpmarc.net>
 * @copyright 2016 Matt Schraeder
 * @license   http://www.gnu.org/licenses/gpl-3.0.html  GPL License 3
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MARC;
using System.Data.SQLite;
using System.IO;

namespace CSharp_MARC_Editor
{
    public partial class MainForm : Form
    {
        #region Private member variables

        private FileMARCReader marcRecords;
        public static string connectionString = "Data Source=MARC.db;Version=3";

        private string reloadingDB = "Reloading Database...";
        private string committingTransaction = "Committing Transaction...";
        private bool startEdit = false;
        private bool loading = true;
        private bool reloadFields = false;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }

        #region Utilities

        /// <summary>
        /// Gets the marc record row for inserting into the Record List Data Table
        /// </summary>
        /// <param name="record">The record.</param>
        /// <returns></returns>
        private DataRow GetMARCRecordRow(Record record)
        {
            DataRow newRow = marcDataSet.Tables["Records"].NewRow();

            DataField record100 = (DataField)record["100"];
            DataField record245 = (DataField)record["245"];
            string author = "";
            string title = "";

            if (record100 != null && record100['a'] != null)
                author = record100['a'].Data;
            else if (record245 != null && record245['c'] != null)
                author += " " + record245['c'].Data;

            if (record245 != null && record245['a'] != null)
                title = record245['a'].Data;
            else
                title = string.Empty;

            if (record245 != null && record245['b'] != null)
                title += " " + record245['b'].Data;

            newRow["DateAdded"] = new DateTime();
            newRow["DateChanged"] = DBNull.Value;
            newRow["Author"] = author;
            newRow["Title"] = title;

            return newRow;
        }

        /// <summary>
        /// Loads the field.
        /// </summary>
        /// <param name="recordID">The record identifier.</param>
        private void LoadFields(int recordID)
        {
            marcDataSet.Tables["Fields"].Rows.Clear();

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Fields where RecordiD = @RecordID";
                
                using (SQLiteCommand command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.Add("@RecordID", DbType.Int32).Value = recordID;
                    SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(command);
                    dataAdapter.Fill(marcDataSet, "Fields");
                    fieldsDataGridView.DataSource = marcDataSet.Tables["Fields"];
                }

                foreach (DataGridViewRow row in fieldsDataGridView.Rows)
                {
                    if (!row.IsNewRow && row.Cells[2].Value.ToString().StartsWith("00"))
                    {
                        row.Cells[3].Value = "-";
                        row.Cells[4].Value = "-";
                    }
                }
            }

            if (fieldsDataGridView.Rows.Count > 0)
            {
                DataGridViewCellEventArgs args = new DataGridViewCellEventArgs(0, 0);
                fieldsDataGridView_CellClick(this, args);
            }

            LoadPreview(recordID);
            splitContainer.Panel2.Enabled = true;
        }

        /// <summary>
        /// Loads the subfield.
        /// </summary>
        /// <param name="FieldID">The field identifier.</param>
        private void LoadSubfields(int FieldID)
        {
            marcDataSet.Tables["Subfields"].Rows.Clear();
            codeDataGridViewTextBoxColumn.Visible = true;
            subfieldsDataGridView.AllowUserToAddRows = true;

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Subfields where FieldID = @FieldID";

                using (SQLiteCommand command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.Add("@FieldID", DbType.Int32).Value = FieldID;
                    SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(command);
                    dataAdapter.Fill(marcDataSet, "Subfields");
                    subfieldsDataGridView.DataSource = marcDataSet.Tables["Subfields"];
                }
            }
        }

        /// <summary>
        /// Loads the preview.
        /// </summary>
        /// <param name="recordID">The record identifier.</param>
        private void LoadPreview(int recordID)
        {
            Record record = new Record();

            using (SQLiteCommand fieldsCommand = new SQLiteCommand("SELECT * FROM Fields WHERE RecordID = @RecordID ORDER BY FieldID", new SQLiteConnection(connectionString)))
            {
                fieldsCommand.Connection.Open();
                fieldsCommand.Parameters.Add("@RecordID", DbType.Int32);

                using (SQLiteCommand subfieldsCommand = new SQLiteCommand("SELECT * FROM Subfields WHERE FieldID = @FieldID ORDER BY SubfieldID", new SQLiteConnection(connectionString)))
                {
                    subfieldsCommand.Connection.Open();
                    subfieldsCommand.Parameters.Add("@FieldID", DbType.Int32);
                    fieldsCommand.Parameters["@RecordID"].Value = recordID;

                    using (SQLiteDataReader fieldsReader = fieldsCommand.ExecuteReader())
                    {
                        while (fieldsReader.Read())
                        {
                            if (fieldsReader["TagNumber"].ToString().StartsWith("00"))
                            {
                                ControlField controlField = new ControlField(fieldsReader["TagNumber"].ToString(), fieldsReader["ControlData"].ToString());
                                record.InsertField(controlField);
                            }
                            else
                            {
                                char ind1 = ' ';
                                char ind2 = ' ';

                                if (fieldsReader["Ind1"].ToString().Length > 0)
                                    ind1 = fieldsReader["Ind1"].ToString()[0];

                                if (fieldsReader["Ind2"].ToString().Length > 0)
                                    ind2 = fieldsReader["Ind2"].ToString()[0];

                                DataField dataField = new DataField(fieldsReader["TagNumber"].ToString(), new List<Subfield>(), ind1, ind2);
                                subfieldsCommand.Parameters["@FieldID"].Value = fieldsReader["FieldID"];

                                using (SQLiteDataReader subfieldReader = subfieldsCommand.ExecuteReader())
                                {
                                    while (subfieldReader.Read())
                                    {
                                        dataField.InsertSubfield(new Subfield(subfieldReader["Code"].ToString()[0], subfieldReader["Data"].ToString()));
                                    }
                                }

                                record.InsertField(dataField);
                            }
                        }
                    }
                }
            }

            previewTextBox.Text = record.ToString();
        }

        /// <summary>
        /// Loads the control field.
        /// </summary>
        /// <param name="FieldID">The field identifier.</param>
        /// <param name="data">The data.</param>
        private void LoadControlField(int FieldID, string data)
        {
            marcDataSet.Tables["Subfields"].Rows.Clear();
            codeDataGridViewTextBoxColumn.Visible = false;
            subfieldsDataGridView.AllowUserToAddRows = false;
            DataRow newRow = marcDataSet.Tables["Subfields"].NewRow();
            newRow["FieldID"] = FieldID;
            newRow["Code"] = "";
            newRow["Data"] = data;
            marcDataSet.Tables["Subfields"].Rows.Add(newRow);
        }

        /// <summary>
        /// Resets the database.
        /// </summary>
        /// <param name="force">if set to <c>true</c> [force].</param>
        private void ResetDatabase(bool force = false)
        {
            if (force || MessageBox.Show("This will permanently delete all records, and recreate the database." + Environment.NewLine + Environment.NewLine + "Are you sure you want to reset the database?", "Are you sure you want to reset the database?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                this.Enabled = false;

                GC.Collect();
                GC.WaitForFullGCComplete();

                if (File.Exists("MARC.db"))
                    File.Delete("MARC.db");

                using (SQLiteCommand command = new SQLiteCommand(new SQLiteConnection(connectionString)))
                {
                    command.Connection.Open();

                    command.CommandText = @"CREATE TABLE [Fields](
                                                [FieldID] integer PRIMARY KEY ASC AUTOINCREMENT NOT NULL, 
                                                [RecordID] nvarchar(2147483647) NOT NULL, 
                                                [TagNumber] nvarchar(2147483647) NOT NULL, 
                                                [Ind1] char, 
                                                [Ind2] char, 
                                                [ControlData] nvarchar(2147483647), 
                                                FOREIGN KEY([RecordID]) REFERENCES Records([RecordID]) ON DELETE CASCADE ON UPDATE RESTRICT);

                                            CREATE TABLE [Records](
                                                [RecordID] integer PRIMARY KEY ASC AUTOINCREMENT NOT NULL, 
                                                [DateAdded] datetime NOT NULL, 
                                                [DateChanged] datetime, 
                                                [Author] nvarchar(2147483647), 
                                                [Title] nvarchar(2147483647), 
                                                [Barcode] nvarchar(2147483647), 
                                                [Classification] nvarchar(2147483647), 
                                                [MainEntry] nvarchar(2147483647));

                                            CREATE TABLE [Subfields](
                                                [SubfieldID] integer PRIMARY KEY ASC AUTOINCREMENT NOT NULL, 
                                                [FieldID] bigint NOT NULL, 
                                                [Code] char NOT NULL, 
                                                [Data] nvarchar(2147483647) NOT NULL, 
                                                FOREIGN KEY([FieldID]) REFERENCES Fields([FieldID]) ON DELETE CASCADE ON UPDATE RESTRICT);

                                            CREATE INDEX [FieldID]
                                            ON [Subfields](
                                                [FieldID] ASC);

                                            CREATE INDEX [RecordID]
                                            ON [Fields](
                                                [RecordID] ASC);";

                    command.ExecuteNonQuery();
                }

                this.OnLoad(new EventArgs());
                this.Enabled = true;
            }
        }

        /// <summary>
        /// Rebuilds the records preview information.
        /// This consists of the Author, Title, Barcode, Classification, and MainEntry fields
        /// </summary>
        private void RebuildRecordsPreviewInformation(int? recordID = null)
        {
            using (SQLiteConnection readerConnection = new SQLiteConnection(connectionString))
            {
                readerConnection.Open();

                using (SQLiteCommand readerCommand = new SQLiteCommand(readerConnection))
                {
                    StringBuilder query = new StringBuilder("SELECT r.RecordID as RecordID, TagNumber, Code, Data, Author, Title, Barcode, Classification, MainEntry FROM Records r LEFT OUTER JOIN Fields f ON r.RecordID = f.RecordID LEFT OUTER JOIN Subfields s ON f.FieldID = s.FieldID");

                    if (recordID.HasValue)
                    {
                        query.Append(" WHERE r.RecordID = @RecordID");
                        readerCommand.Parameters.Add("@RecordID", DbType.Int32).Value = recordID;
                    }

                    query.Append(" UNION SELECT '-2' as RecordID, '', '', '', '', '', '', '', ''");
                    query.Append(" ORDER BY RecordID, TagNumber, Code");

                    readerCommand.CommandText = query.ToString();

                    using (SQLiteConnection updaterConnection = new SQLiteConnection(connectionString))
                    {
                        updaterConnection.Open();

                        using (SQLiteCommand updaterCommand = new SQLiteCommand(updaterConnection))
                        {
                            updaterCommand.CommandText = "BEGIN;";
                            updaterCommand.ExecuteNonQuery();

                            updaterCommand.CommandText = "UPDATE Records SET DateChanged = @DateChanged, Author = @Author, Title = @Title, Barcode = @Barcode, Classification = @Classification, MainEntry = @MainEntry WHERE RecordID = @RecordID";
                            
                            updaterCommand.Parameters.Add("@Author", DbType.String);
                            updaterCommand.Parameters.Add("@Title", DbType.String);
                            updaterCommand.Parameters.Add("@Barcode", DbType.String);
                            updaterCommand.Parameters.Add("@Classification", DbType.String);
                            updaterCommand.Parameters.Add("@MainEntry", DbType.String);
                            updaterCommand.Parameters.Add("@RecordID", DbType.Int32);
                            updaterCommand.Parameters.Add("@DateChanged", DbType.DateTime);

                            using (SQLiteDataReader reader = readerCommand.ExecuteReader())
                            {
                                int currentRecord = -1;

                                string author = null;
                                string title = null;
                                string barcode = "";
                                string classification = "";
                                string mainEntry = "";

                                while (reader.Read())
                                {

                                    if (currentRecord != Int32.Parse(reader["RecordID"].ToString()))
                                    {
                                        if (currentRecord >= 0)
                                        {
                                            updaterCommand.Parameters["@DateChanged"].Value = DateTime.Now;
                                            updaterCommand.Parameters["@Author"].Value = author;
                                            updaterCommand.Parameters["@Title"].Value = title;
                                            updaterCommand.Parameters["@Barcode"].Value = barcode;
                                            updaterCommand.Parameters["@Classification"].Value = classification;
                                            updaterCommand.Parameters["@MainEntry"].Value = mainEntry;
                                            updaterCommand.Parameters["@RecordID"].Value = currentRecord;

                                            updaterCommand.ExecuteNonQuery();

                                            if (recordID != null)
                                            {
                                                foreach (DataGridViewRow row in recordsDataGridView.Rows)
                                                {
                                                    if (Int32.Parse(row.Cells[0].Value.ToString()) == recordID)
                                                    {
                                                        row.Cells[2].Value = updaterCommand.Parameters["@DateChanged"].Value.ToString();
                                                        row.Cells[3].Value = author;
                                                        row.Cells[4].Value = title;
                                                        row.Cells[5].Value = barcode;
                                                        row.Cells[6].Value = classification;
                                                        row.Cells[7].Value = mainEntry;
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        currentRecord = Int32.Parse(reader["RecordID"].ToString());

                                        author = null;
                                        title = null;
                                        barcode = "";
                                        classification = "";
                                        mainEntry = "";
                                    }

                                    if (author == null && (string)reader["TagNumber"] == "100" && (string)reader["Code"] == "a")
                                        author = (string)reader["Data"];
                                    else if (author == null && (string)reader["TagNumber"] == "245" && (string)reader["Code"] == "c")
                                        author = (string)reader["Data"];

                                    if (title == null && (string)reader["TagNumber"] == "245" && (string)reader["Code"] == "a")
                                        title = (string)reader["Data"];
                                    else if (title == null && (string)reader["TagNumber"] == "245" && (string)reader["Code"] == "b")
                                        title += " " + (string)reader["Data"];
                                }
                            }
                            
                            updaterCommand.CommandText = "END;";
                            updaterCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        #endregion

        #region Form Events

        /// <summary>
        /// Handles the Load event of the MainForm control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                loading = true;

                recordsDataGridView.DataSource = null;
                fieldsDataGridView.DataSource = null;
                subfieldsDataGridView.DataSource = null;

                marcDataSet.Tables["Records"].Rows.Clear();
                marcDataSet.Tables["Fields"].Rows.Clear();
                marcDataSet.Tables["Subfields"].Rows.Clear();

                //MessageBox.Show((Convert.ToDateTime("4/19/2016 7:09:06 PM") - Convert.ToDateTime("4/19/2016 6:13:04 PM")).TotalSeconds.ToString());
                using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Records", connection))
                    {
                        SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(command);
                        dataAdapter.Fill(marcDataSet, "Records");
                        recordsDataGridView.DataSource = marcDataSet.Tables["Records"];
                    }

                    using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Fields WHERE 1 = 0", connection))
                    {
                        SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(command);
                        dataAdapter.Fill(marcDataSet, "Fields");
                        fieldsDataGridView.DataSource = marcDataSet.Tables["Fields"];
                    }

                    using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Subfields WHERE 1 = 0", connection))
                    {
                        SQLiteDataAdapter recordsDataAdapter = new SQLiteDataAdapter(command);
                        recordsDataAdapter.Fill(marcDataSet, "Subfields");
                        subfieldsDataGridView.DataSource = marcDataSet.Tables["Subfields"];
                    }
                }

                if (recordsDataGridView.Rows.Count > 0)
                {
                    DataGridViewCellEventArgs args = new DataGridViewCellEventArgs(0, 0);
                    recordsDataGridView_CellClick(this, args);
                }

                loading = false;
            }
            catch (Exception ex)
            {
                if (MessageBox.Show("Error loading database. " + ex.Message + Environment.NewLine + Environment.NewLine + "If you continue to see this message, it may be necessary to reset the database. Doing so will permanently delete all records from the database." + Environment.NewLine + Environment.NewLine + "Do you want to reset the database?", "Error loading database.", MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    ResetDatabase(true);
                }
                else
                {
                    this.Close();
                }
            }
        }

        #region Loading Records

        /// <summary>
        /// Handles the CellClick event of the recordsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellEventArgs"/> instance containing the event data.</param>
        private void recordsDataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow rowClicked = recordsDataGridView.Rows[e.RowIndex];
                if (!rowClicked.IsNewRow)
                    LoadFields(Int32.Parse(rowClicked.Cells[0].Value.ToString()));
                else
                    fieldsDataGridView.Rows.Clear();
            }
        }

        /// <summary>
        /// Handles the CellClick event of the fieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellEventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow rowClicked = fieldsDataGridView.Rows[e.RowIndex];
                if (!rowClicked.IsNewRow && rowClicked.Cells[0].Value.ToString() != "")
                {
                    if (rowClicked.Cells[2].Value.ToString().StartsWith("00"))
                        LoadControlField(Int32.Parse(rowClicked.Cells[0].Value.ToString()), rowClicked.Cells[5].Value.ToString());
                    else
                        LoadSubfields(Int32.Parse(rowClicked.Cells[0].Value.ToString()));
                }
                else
                    marcDataSet.Tables["Subfields"].Clear();
            }
        }

        #endregion

        #region Importing Records

        /// <summary>
        /// Handles the Click event of the openToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.Enabled = false;
                toolStripProgressBar.Style = ProgressBarStyle.Marquee;
                toolStripProgressBar.MarqueeAnimationSpeed = 30;
                toolStripProgressBar.Enabled = true;
                toolStripProgressBar.Visible = true;
                progressToolStripStatusLabel.Visible = true;
                recordsDataGridView.SuspendLayout();
                recordsDataGridView.DataSource = null;
                importingBackgroundWorker.RunWorkerAsync(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// Handles the DoWork event of the loadingBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        private void importingBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            marcRecords = new FileMARCReader(e.Argument.ToString());

            int i = 0;

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    command.CommandText = "BEGIN";
                    command.ExecuteNonQuery();

                    foreach (Record record in marcRecords)
                    {
                        i++;
                        DataRow newRow = GetMARCRecordRow(record);
                        
                        command.CommandText = "INSERT INTO Records (DateAdded, DateChanged, Author, Title, Barcode, Classification, MainEntry) VALUES (@DateAdded, @DateChanged, @Author, @Title, @Barcode, @Classification, @MainEntry)";
                        command.Parameters.Add("@DateAdded", DbType.DateTime).Value = DateTime.Now;
                        command.Parameters.Add("@DateChanged", DbType.DateTime).Value = DBNull.Value;
                        command.Parameters.Add("@Author", DbType.String).Value = newRow["Author"];
                        command.Parameters.Add("@Title", DbType.String).Value = newRow["Title"];
                        command.Parameters.Add("@Barcode", DbType.String).Value = newRow["Barcode"];
                        command.Parameters.Add("@Classification", DbType.String).Value = newRow["Classification"];
                        command.Parameters.Add("@MainEntry", DbType.String).Value = newRow["MainEntry"];

                        command.ExecuteNonQuery();
                        
                        int recordID = (int)connection.LastInsertRowId;

                        foreach (Field field in record.Fields)
                        {
                            command.CommandText = "INSERT INTO Fields (RecordID, TagNumber, Ind1, Ind2, ControlData) VALUES (@RecordID, @TagNumber, @Ind1, @Ind2, @ControlData)";
                            command.Parameters.Add("@RecordID", DbType.Int32).Value = recordID;
                            command.Parameters.Add("@TagNumber", DbType.String).Value = field.Tag;
                            if (field.IsDataField())
                            {
                                command.Parameters.Add("@Ind1", DbType.String).Value = ((DataField)field).Indicator1;
                                command.Parameters.Add("@Ind2", DbType.String).Value = ((DataField)field).Indicator2;
                                command.Parameters.Add("@ControlData", DbType.String).Value = DBNull.Value;
                                
                                command.ExecuteNonQuery();
                                
                                int fieldID = (int)connection.LastInsertRowId;

                                foreach (Subfield subfield in ((DataField)field).Subfields)
                                {
                                    command.CommandText = "INSERT INTO Subfields (FieldID, Code, Data) VALUES (@FieldID, @Code, @Data)";
                                    command.Parameters.Add("@FieldID", DbType.Int32).Value = fieldID;
                                    command.Parameters.Add("@Code", DbType.String).Value = subfield.Code;
                                    command.Parameters.Add("@Data", DbType.String).Value = subfield.Data;
                                    command.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                command.Parameters.Add("@Ind1", DbType.String).Value = DBNull.Value;
                                command.Parameters.Add("@Ind2", DbType.String).Value = DBNull.Value;
                                command.Parameters.Add("@ControlData", DbType.String).Value = ((ControlField)field).Data;
                                command.ExecuteNonQuery();
                            }
                        }
                        command.Parameters.Clear();

                        importingBackgroundWorker.ReportProgress(i);
                    }

                    i = -2;
                    importingBackgroundWorker.ReportProgress(i);

                    command.CommandText = "END";
                    command.ExecuteNonQuery();
                }

                i = -1;
                importingBackgroundWorker.ReportProgress(i);
            }
        }

        /// <summary>
        /// Handles the ProgressChanged event of the loadingBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void importingBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            switch (e.ProgressPercentage)
            {
                case -1:
                    progressToolStripStatusLabel.Text = reloadingDB;
                    break;
                case -2:
                    progressToolStripStatusLabel.Text = committingTransaction;
                    break;
                default:
                    progressToolStripStatusLabel.Text = e.ProgressPercentage.ToString();
                    break;
            }
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the loadingBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void importingBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            marcDataSet.Tables["Records"].Rows.Clear();

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Records", connection))
                {
                    SQLiteDataAdapter recordsDataAdapter = new SQLiteDataAdapter(command);
                    recordsDataAdapter.Fill(marcDataSet, "Records");
                    SQLiteCommandBuilder commandBuilder = new SQLiteCommandBuilder(recordsDataAdapter);
                    recordsDataAdapter.InsertCommand = commandBuilder.GetInsertCommand();
                    recordsDataGridView.DataSource = marcDataSet.Tables["Records"];
                }
            }

            if (recordsDataGridView.Rows.Count > 0)
            {
                DataGridViewCellEventArgs args = new DataGridViewCellEventArgs(0, 0);
                recordsDataGridView_CellClick(this, args);
            }

            progressToolStripStatusLabel.Text = "";
            toolStripProgressBar.Visible = false;
            toolStripProgressBar.Enabled = false;
            progressToolStripStatusLabel.Visible = false;
            toolStripProgressBar.MarqueeAnimationSpeed = 0;
            recordsDataGridView.DataSource = marcDataSet.Tables["Records"];
            recordsDataGridView.ResumeLayout();
            loading = false;
            this.Enabled = true;
        }

        #endregion

        #region Exporting Records

        /// <summary>
        /// Handles the Click event of the exportRecordsToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void exportRecordsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.Enabled = false;
                toolStripProgressBar.Style = ProgressBarStyle.Continuous;
                toolStripProgressBar.Enabled = true;
                toolStripProgressBar.Visible = true;
                progressToolStripStatusLabel.Visible = true;
                exportingBackgroundWorker.RunWorkerAsync(saveFileDialog.FileName);
            }
        }

        /// <summary>
        /// Handles the DoWork event of the exportingBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        private void exportingBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            using (SQLiteCommand fieldsCommand = new SQLiteCommand("SELECT * FROM Fields WHERE RecordID = @RecordID ORDER BY FieldID", new SQLiteConnection(connectionString)))
            {
                fieldsCommand.Connection.Open();
                fieldsCommand.Parameters.Add("@RecordID", DbType.Int32);

                using (SQLiteCommand subfieldsCommand = new SQLiteCommand("SELECT * FROM Subfields WHERE FieldID = @FieldID ORDER BY SubfieldID", new SQLiteConnection(connectionString)))
                {
                    subfieldsCommand.Connection.Open();
                    subfieldsCommand.Parameters.Add("@FieldID", DbType.Int32);

                    int i = 0;
                    int max = marcDataSet.Tables["Records"].Rows.Count;
                    FileMARCWriter marcWriter = new FileMARCWriter(saveFileDialog.FileName);

                    foreach (DataGridViewRow row in recordsDataGridView.Rows)
                    {
                        Record record = new Record();
                        fieldsCommand.Parameters["@RecordID"].Value = row.Cells[0].Value;

                        using (SQLiteDataReader fieldsReader = fieldsCommand.ExecuteReader())
                        {
                            while (fieldsReader.Read())
                            {
                                if (fieldsReader["TagNumber"].ToString().StartsWith("00"))
                                {
                                    ControlField controlField = new ControlField(fieldsReader["TagNumber"].ToString(), fieldsReader["ControlData"].ToString());
                                    record.InsertField(controlField);
                                }
                                else
                                {
                                    DataField dataField = new DataField(fieldsReader["TagNumber"].ToString(), new List<Subfield>(), fieldsReader["Ind1"].ToString()[0], fieldsReader["Ind2"].ToString()[0]);
                                    subfieldsCommand.Parameters["@FieldID"].Value = fieldsReader["FieldID"];

                                    using (SQLiteDataReader subfieldReader = subfieldsCommand.ExecuteReader())
                                    {
                                        while (subfieldReader.Read())
                                        {
                                            dataField.InsertSubfield(new Subfield(subfieldReader["Code"].ToString()[0], subfieldReader["Data"].ToString()));
                                        }
                                    }

                                    record.InsertField(dataField);
                                }
                            }
                        }

                        marcWriter.Write(record);
                        exportingBackgroundWorker.ReportProgress(i / max);
                    }

                    marcWriter.WriteEnd();
                    marcWriter.Dispose();
                }
            }
        }

        /// <summary>
        /// Handles the ProgressChanged event of the exportingBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void exportingBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressToolStripStatusLabel.Text = e.ProgressPercentage.ToString();
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the exportingBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void exportingBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressToolStripStatusLabel.Text = "";
            toolStripProgressBar.Visible = false;
            toolStripProgressBar.Enabled = false;
            progressToolStripStatusLabel.Visible = false;
            toolStripProgressBar.MarqueeAnimationSpeed = 0;
            loading = false;
            this.Enabled = true;

        }

        #endregion

        #region Editing Cells

        /// <summary>
        /// Handles the CellValidating event of the fieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellValidatingEventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            try
            {
                if (!fieldsDataGridView.Rows[e.RowIndex].IsNewRow && startEdit)
                {
                    string query = "UPDATE fields SET ";
                    switch (e.ColumnIndex)
                    {
                        case 2:
                            if (fieldsDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString().StartsWith("00") && !e.FormattedValue.ToString().StartsWith("00"))
                                throw new Exception("Cannot change a control field to a data field.");
                            else if (!fieldsDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString().StartsWith("00") && e.FormattedValue.ToString().StartsWith("00"))
                                throw new Exception("Cannot change a data field to a control field.");
                            else if (Field.ValidateTag(e.FormattedValue.ToString()))
                                query += "tagnumber = @Value ";
                            else
                                throw new Exception("Invalid tag number.");
                            break;
                        case 3:
                            if (e.FormattedValue.ToString().Length == 1 && DataField.ValidateIndicator(e.FormattedValue.ToString()[0]))
                                query += "ind1 = @Value ";
                            else
                                throw new Exception("Invalid indicator.");
                            break;
                        case 4:
                            if (e.FormattedValue.ToString().Length == 1 && DataField.ValidateIndicator(e.FormattedValue.ToString()[0]))
                                query += "ind2 = @Value ";
                            else
                                throw new Exception("Invalid indicator.");
                            break;
                        case 5:
                            query += "controldata = @Value ";
                            break;
                        default:
                            e.Cancel = true;
                            fieldsDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "Error 001 - This should never happen. ColumnIndex: " + e.ColumnIndex;
                            return;
                    }

                    query += "WHERE FieldID = @FieldID";

                    using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                    {
                        connection.Open();

                        using (SQLiteCommand command = new SQLiteCommand(query, connection))
                        {
                            command.Parameters.Add("@Value", DbType.String).Value = e.FormattedValue;
                            command.Parameters.Add("@FieldID", DbType.String).Value = fieldsDataGridView.Rows[e.RowIndex].Cells[0].Value.ToString();

                            command.ExecuteNonQuery();
                        }
                    }

                    RebuildRecordsPreviewInformation(Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString()));
                }
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                errorProvider.SetError((Control)fieldsDataGridView.EditingControl, ex.Message);
                errorProvider.SetIconAlignment((Control)fieldsDataGridView.EditingControl, ErrorIconAlignment.MiddleRight);
                errorProvider.SetIconPadding((Control)fieldsDataGridView.EditingControl, -20);
            }
        }

        /// <summary>
        /// Handles the CellValidating event of the subfieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellValidatingEventArgs"/> instance containing the event data.</param>
        private void subfieldsDataGridView_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            try
            {
                if (!subfieldsDataGridView.Rows[e.RowIndex].IsNewRow && startEdit)
                {
                    if (subfieldsDataGridView.Rows[e.RowIndex].Cells[2].Visible)
                    {
                        string query = "UPDATE subfields SET ";

                        switch (e.ColumnIndex)
                        {
                            case 2:
                                if (e.FormattedValue.ToString().Length == 1 && DataField.ValidateIndicator(e.FormattedValue.ToString()[0]))
                                {
                                    query += "code = @Value ";
                                }
                                else
                                    throw new Exception("Invalid subfield code.");
                                break;
                            case 3:
                                query += "data = @Value ";
                                break;
                            default:
                                throw new Exception("Error 002 - This should never happen. ColumnIndex: " + e.ColumnIndex);
                        }

                        query += "WHERE SubfieldID = @SubfieldID";

                        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                        {
                            connection.Open();

                            using (SQLiteCommand command = new SQLiteCommand(query, connection))
                            {
                                command.Parameters.Add("@Value", DbType.String).Value = e.FormattedValue;
                                command.Parameters.Add("@SubfieldID", DbType.String).Value = subfieldsDataGridView.Rows[e.RowIndex].Cells[0].Value.ToString();

                                command.ExecuteNonQuery();
                            }
                        }
                    }
                    else //It's a control field -> we need to update the field row instead
                    {
                        string query = "UPDATE fields SET ";

                        switch (e.ColumnIndex)
                        {
                            case 3:
                                query += "controldata = @Value ";
                                break;
                            default:
                                throw new Exception("Error 003 - This should never happen. ColumnIndex: " + e.ColumnIndex);
                        }

                        query += "WHERE FieldID = @FieldID";

                        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                        {
                            connection.Open();

                            using (SQLiteCommand command = new SQLiteCommand(query, connection))
                            {
                                command.Parameters.Add("@Value", DbType.String).Value = e.FormattedValue;
                                command.Parameters.Add("@FieldID", DbType.String).Value = subfieldsDataGridView.Rows[e.RowIndex].Cells[1].Value.ToString();

                                command.ExecuteNonQuery();
                                reloadFields = true;
                            }
                        }
                    }

                    RebuildRecordsPreviewInformation(Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString()));
                }
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                errorProvider.SetError((Control)subfieldsDataGridView.EditingControl, ex.Message);
                errorProvider.SetIconAlignment((Control)subfieldsDataGridView.EditingControl, ErrorIconAlignment.MiddleRight);
                errorProvider.SetIconPadding((Control)subfieldsDataGridView.EditingControl, -20);
            }
        }

        /// <summary>
        /// Handles the CellBeginEdit event of the fieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellCancelEventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (!loading && fieldsDataGridView.Rows[e.RowIndex].Cells[0].Value.ToString() != "")
            {
                switch (e.ColumnIndex)
                {
                    case 2:
                        break;
                    case 3:
                    case 4:
                        string tagNumber = fieldsDataGridView.Rows[e.RowIndex].Cells[2].Value.ToString();
                        if (tagNumber.StartsWith("00") || tagNumber == "")
                        {
                            MessageBox.Show("Cannot edit indicators on control fields.");
                            startEdit = false;
                            e.Cancel = true;
                        }
                        break;
                    default:
                        throw new Exception("Error 004 - This should never happen. Column: " + e.ColumnIndex);
                }
            }
        }

        /// <summary>
        /// Handles the CellBeginEdit event of the subfieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellCancelEventArgs"/> instance containing the event data.</param>
        private void subfieldsDataGridView_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (!loading && subfieldsDataGridView.Rows[e.RowIndex].Cells[0].Value.ToString() != "")
                startEdit = true;
        }

        /// <summary>
        /// Handles the CellValidated event of the subfieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellEventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            startEdit = false;
            fieldsDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
        }

        /// <summary>
        /// Handles the CellValidated event of the subfieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellEventArgs"/> instance containing the event data.</param>
        private void subfieldsDataGridView_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            if (reloadFields && !subfieldsDataGridView.Rows[e.RowIndex].Cells[2].Visible)
            {
                reloadFields = false;
                recordsDataGridView_CellClick(sender, new DataGridViewCellEventArgs(recordsDataGridView.SelectedCells[0].ColumnIndex, recordsDataGridView.SelectedCells[0].RowIndex));
            }

            startEdit = false;
            subfieldsDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
        }

        /// <summary>
        /// Handles the CellEndEdit event of the fieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellEventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            startEdit = false;
            fieldsDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
            LoadPreview(Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString()));
        }

        /// <summary>
        /// Handles the CellEndEdit event of the subfieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellEventArgs"/> instance containing the event data.</param>
        private void subfieldsDataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            startEdit = false;
            subfieldsDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
            LoadPreview(Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString()));
        }

        #endregion

        #region Adding Rows

        /// <summary>
        /// Handles the Click event of the createBlankRecordToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void createBlankRecordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string query = "INSERT INTO Records (DateAdded, Author, Title) VALUES (CURRENT_DATE, 'New Record', 'New Record')";

                using (SQLiteCommand command = new SQLiteCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                    int recordID = (int)connection.LastInsertRowId;

                    this.OnLoad(new EventArgs());
                    recordsDataGridView.Rows[recordsDataGridView.Rows.Count - 1].Cells[0].Selected = true;
                }
            }
        }

        private void fieldsDataGridView_UserAddedRow(object sender, DataGridViewRowEventArgs e)
        {
            int recordID = Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString());
            e.Row.Cells[1].Value = recordID;
        }

        /// <summary>
        /// Handles the RowValidating event of the fieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellCancelEventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (!loading && !fieldsDataGridView.Rows[e.RowIndex].IsNewRow && fieldsDataGridView.Rows[e.RowIndex].Cells[0].Value.ToString() == "")
            {
                try
                {
                    int recordID = Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString());
                    string tagNumber = fieldsDataGridView.Rows[e.RowIndex].Cells[2].Value.ToString();
                    string ind1 = fieldsDataGridView.Rows[e.RowIndex].Cells[3].Value.ToString();
                    string ind2 = fieldsDataGridView.Rows[e.RowIndex].Cells[4].Value.ToString();

                    if (!Field.ValidateTag(tagNumber) || (tagNumber.StartsWith("00") && (ind1 != "" || ind2 != "")))
                    {
                        e.Cancel = true;
                        return;
                    }

                    using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                    {
                        connection.Open();

                        string query = "INSERT INTO Fields (RecordID, TagNumber, Ind1, Ind2) VALUES (@RecordID, @TagNumber, @Ind1, @Ind2)";

                        using (SQLiteCommand command = new SQLiteCommand(query, connection))
                        {
                            command.Parameters.Add("@RecordID", DbType.Int32).Value = recordID;
                            command.Parameters.Add("@TagNumber", DbType.String).Value = tagNumber;
                            command.Parameters.Add("@Ind1", DbType.String).Value = ind1;
                            command.Parameters.Add("@Ind2", DbType.String).Value = ind2;

                            command.ExecuteNonQuery();
                            LoadFields(recordID);
                            RebuildRecordsPreviewInformation(recordID);
                        }
                    }
                }
                catch (Exception)
                {
                    e.Cancel = true;
                }
            }
        }

        /// <summary>
        /// Handles the RowValidating event of the subfieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewCellCancelEventArgs"/> instance containing the event data.</param>
        private void subfieldsDataGridView_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (!loading && !subfieldsDataGridView.Rows[e.RowIndex].IsNewRow && subfieldsDataGridView.Rows[e.RowIndex].Cells[0].Value.ToString() == "")
            {
                try
                {
                    int recordID = Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString());
                    int fieldID = Int32.Parse(fieldsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString());
                    string code = subfieldsDataGridView.Rows[e.RowIndex].Cells[2].Value.ToString();
                    string data = subfieldsDataGridView.Rows[e.RowIndex].Cells[3].Value.ToString();

                    using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                    {
                        connection.Open();

                        string query = "INSERT INTO Subfields (FieldID, Code, Data) VALUES (@FieldID, @Code, @Data)";

                        using (SQLiteCommand command = new SQLiteCommand(query, connection))
                        {
                            command.Parameters.Add("@FieldID", DbType.Int32).Value = fieldID;
                            command.Parameters.Add("@Code", DbType.String).Value = code;
                            command.Parameters.Add("@Data", DbType.String).Value = data;

                            command.ExecuteNonQuery();
                            LoadSubfields(fieldID);
                            RebuildRecordsPreviewInformation(recordID);
                        }
                    }
                }
                catch (Exception)
                {
                    e.Cancel = true;
                }
            }
        }

        #endregion

        #region Deleting Rows

        /// <summary>
        /// Handles the UserDeletingRow event of the recordsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewRowCancelEventArgs"/> instance containing the event data.</param>
        private void recordsDataGridView_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (e.Row.Cells[0].Value.ToString() != "")
            {
                using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string query = "DELETE FROM Records WHERE RecordID = @RecordID";

                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.Add("@RecordID", DbType.Int32).Value = Int32.Parse(e.Row.Cells[0].Value.ToString());
                        command.ExecuteNonQuery();
                    }

                    RebuildRecordsPreviewInformation(Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString()));
                }
            }
        }

        /// <summary>
        /// Handles the UserDeletingRow event of the fieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewRowCancelEventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (e.Row.Cells[0].Value.ToString() != "")
            {
                using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string query = "DELETE FROM Fields WHERE FieldID = @FieldID";

                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.Add("@FieldID", DbType.Int32).Value = Int32.Parse(e.Row.Cells[0].Value.ToString());
                        command.ExecuteNonQuery();
                    }

                    RebuildRecordsPreviewInformation(Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString()));
                }
            }
        }

        /// <summary>
        /// Handles the UserDeletingRow event of the subfieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridViewRowCancelEventArgs"/> instance containing the event data.</param>
        private void subfieldsDataGridView_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (e.Row.Cells[0].Value.ToString() != "")
            {
                using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string query = "DELETE FROM Subfields WHERE SubfieldID = @SubfieldID";

                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.Add("@SubfieldID", DbType.Int32).Value = Int32.Parse(e.Row.Cells[0].Value.ToString());
                        command.ExecuteNonQuery();
                    }

                    RebuildRecordsPreviewInformation(Int32.Parse(recordsDataGridView.SelectedCells[0].OwningRow.Cells[0].Value.ToString()));
                }
            }
        }

        #endregion

        #region Batch Editing

        /// <summary>
        /// Handles the Click event of the findAndReplaceToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void findAndReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FindReplaceForm form = new FindReplaceForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    this.Enabled = false;

                    using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                    {
                        connection.Open();
                        using (SQLiteCommand command = new SQLiteCommand(connection))
                        {
                            StringBuilder query = new StringBuilder("UPDATE Subfields SET Data = ");

                            if (form.CaseSensitive)
                            {
                                query.Append("REPLACE(Data, @ReplaceData, @ReplaceWith)");
                                query.Insert(0, "PRAGMA case_sensitive_like=ON;");
                            }
                            else
                            {
                                query.Append("(SUBSTR(Data, 0, INSTR(Data, @ReplaceData)) || @ReplaceWith || SUBSTR(Data, INSTR(Data, @ReplaceData) + LENGTH(@ReplaceData)))");
                            }

                            command.Parameters.Add("@ReplaceData", DbType.String).Value = form.Data;
                            command.Parameters.Add("@ReplaceWith", DbType.String).Value = form.ReplaceWith;

                            StringBuilder whereClause = new StringBuilder(" WHERE ");

                            if (form.SelectedTags.Contains("Any"))
                            {
                                // Do nothing!
                            } 
                            else if (form.SelectedTags.Count == 1)
                            {
                                whereClause.Append("FieldID IN (SELECT FieldID FROM Fields WHERE TagNumber = @TagNumber) AND ");
                                command.Parameters.Add("@TagNumber", DbType.String).Value = form.SelectedTags[0];
                            }
                            else if (form.SelectedTags.Count > 1)
                            {
                                int i = 0;
                                whereClause.Append("FieldID IN (SELECT FieldID FROM Fields WHERE TagNumber IN (");

                                foreach (string tag in form.SelectedTags)
                                {
                                    string tagNumber = string.Format("@TagNumber{0}", i);
                                    command.Parameters.Add(tagNumber, DbType.String).Value = tag;
                                    whereClause.AppendFormat("{0}, ", tagNumber);

                                    i++;
                                }

                                whereClause.Remove(whereClause.Length - 2, 2);
                                whereClause.Append(") AND ");
                            }

                            if (form.SelectedIndicator1s.Contains("Any"))
                            {
                                // Do nothing!
                            }
                            else if (form.SelectedIndicator1s.Count == 1)
                            {
                                whereClause.Append("FieldID IN (SELECT FieldID FROM Fields WHERE Ind1 = @Ind1) AND ");
                                command.Parameters.Add("@Ind1", DbType.String).Value = form.SelectedIndicator1s[0];
                            }
                            else if (form.SelectedIndicator1s.Count > 1)
                            {
                                int i = 0;
                                whereClause.Append("FieldID IN (SELECT FieldID FROM Fields WHERE Ind1 IN (");

                                foreach (string ind1 in form.SelectedIndicator1s)
                                {
                                    string indicator = string.Format("@Ind1{0}", i);
                                    command.Parameters.Add(indicator, DbType.String).Value = ind1;
                                    whereClause.AppendFormat("{0}, ", indicator);

                                    i++;
                                }
                                whereClause.Remove(whereClause.Length - 2, 2);
                                whereClause.Append(") AND ");
                            }

                            if (form.SelectedIndicator2s.Contains("Any"))
                            {
                                // Do nothing!
                            }
                            else if (form.SelectedIndicator2s.Count == 1)
                            {
                                whereClause.Append("FieldID IN (SELECT FieldID FROM Fields WHERE Ind2 = @Ind2) AND ");
                                command.Parameters.Add("@Ind2", DbType.String).Value = form.SelectedIndicator2s[0];
                            }
                            else if (form.SelectedIndicator1s.Count > 1)
                            {
                                int i = 0;
                                whereClause.Append("FieldID IN (SELECT FieldID FROM Fields WHERE Ind2 IN (");

                                foreach (string ind2 in form.SelectedIndicator2s)
                                {
                                    string indicator = string.Format("@Ind2{0}", i);
                                    command.Parameters.Add(indicator, DbType.String).Value = ind2;
                                    whereClause.AppendFormat("{0}, ", indicator);

                                    i++;
                                }
                                whereClause.Remove(whereClause.Length - 2, 2);
                                whereClause.Append(") AND ");
                            }

                            if (form.SelectedCodes.Contains("Any"))
                            {
                                // Do nothing!
                            }
                            else if (form.SelectedCodes.Count == 1)
                            {
                                whereClause.Append("Code = @Code AND ");
                                command.Parameters.Add("@Code", DbType.String).Value = form.SelectedCodes[0];
                            }
                            else if (form.SelectedCodes.Count > 1)
                            {
                                int i = 0;
                                whereClause.Append("Code IN (");

                                foreach (string code in form.SelectedCodes)
                                {
                                    string codeParam = string.Format("@Code{0}", i);
                                    command.Parameters.Add(codeParam, DbType.String).Value = code;
                                    whereClause.AppendFormat("{0}, ", codeParam);

                                    i++;
                                }
                                whereClause.Remove(whereClause.Length - 2, 2);
                                whereClause.Append(") AND ");
                            }

                            if (whereClause.ToString() != " WHERE ")
                                whereClause.Remove(whereClause.Length - 4, 4);

                            whereClause.Append("Data LIKE @Data;");
                            command.Parameters.Add("@Data", DbType.String).Value = "%" + form.Data + "%";

                            query.Append(whereClause);
                            query.Append("PRAGMA case_sensitive_like=OFF;");

                            command.CommandText = query.ToString();
                            int count = command.ExecuteNonQuery();
                            MessageBox.Show("Found and replaced " + count + " instances.", "Find and Replace Completed.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    RebuildRecordsPreviewInformation();

                    this.OnLoad(new EventArgs());
                    this.Enabled = true;
                }
            }
        }

        #endregion

        #region Selecting cells

        /// <summary>
        /// Handles the SelectionChanged event of the recordsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void recordsDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (!loading && recordsDataGridView.SelectedCells.Count > 0)
                recordsDataGridView_CellClick(sender, new DataGridViewCellEventArgs(recordsDataGridView.SelectedCells[0].ColumnIndex, recordsDataGridView.SelectedCells[0].RowIndex));
        }

        /// <summary>
        /// Handles the SelectionChanged event of the fieldsDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void fieldsDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (!loading && fieldsDataGridView.SelectedCells.Count > 0)
                fieldsDataGridView_CellClick(sender, new DataGridViewCellEventArgs(fieldsDataGridView.SelectedCells[0].ColumnIndex, fieldsDataGridView.SelectedCells[0].RowIndex));
        }

        #endregion

        #region Misc Events

        /// <summary>
        /// Handles the Click event of the recordListAtTopToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void recordListAtTopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (recordListAtTopToolStripMenuItem.Checked)
            {
                recordListAtTopToolStripMenuItem.Checked = false;
                splitContainer.Orientation = Orientation.Vertical;
            }
            else
            {
                recordListAtTopToolStripMenuItem.Checked = true;
                splitContainer.Orientation = Orientation.Horizontal;
            }
        }

        /// <summary>
        /// Handles the Click event of the clearDatabaseToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void clearDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("This will permanently delete all records from the MARC database." + Environment.NewLine + Environment.NewLine + "Are you sure you want to delete all records?", "Delete all records?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                this.Enabled = false;

                using (SQLiteCommand command = new SQLiteCommand(new SQLiteConnection(connectionString)))
                {
                    command.Connection.Open();
                    command.CommandText = "DELETE FROM Records";
                    command.ExecuteNonQuery();
                }

                this.OnLoad(new EventArgs());
                this.Enabled = true;
            }
        }

        /// <summary>
        /// Handles the Click event of the resetDatabaseToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void resetDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetDatabase();
        }

        /// <summary>
        /// Handles the Click event of the aboutToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutForm form = new AboutForm())
            {
                form.ShowDialog();
            }
        }

        /// <summary>
        /// Handles the Click event of the exitToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
        
        #endregion

        #endregion
    }
}