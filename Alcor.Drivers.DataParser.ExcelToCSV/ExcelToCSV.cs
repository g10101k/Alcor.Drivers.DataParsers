/*
 *  Copyright (C) 2015-2019  Igor Tyulyakov aka g10101k, g101k. Contacts: <g101k@mail.ru>
 *  
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *
 *       http://www.apache.org/licenses/LICENSE-2.0
 *
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Indusoft.Alcor.Drivers.DataParser.GenericParser;
using System.IO;
using System.Data;
using System.Data.OleDb;

namespace Indusoft.Alcor.Drivers.DataParser.ExcelToCSV
{
    [Export(typeof(IGenericParser))]
    public class ExcelToCSV : BaseParser
    {
        Dictionary<string, object> Results = new Dictionary<string, object>();
        /// <summary>
        /// Конструктор драйвера, выполняется единажды при инициализации драйвера
        /// </summary>
        public ExcelToCSV()
        {

        }
        /// <summary>
        /// Возвращает имя драйвера
        /// </summary>
        public override string Name
        {
            get
            {
                return "ExcelToCSV";
            }
        }
        /// <summary>
        /// Разбирает данные полученные Алькором из канала данных
        /// </summary>
        /// <param name="data">Массив байтов полученых из канала данных</param>
        public override void Parse(byte[] data)
        {
            try
            {
                string tmpDirPath = @"C:\LIMS_Tools\Alcor\Temp\";
                string tmpFileName = @"tmp.xls";
                string tmpFilePath = tmpDirPath + tmpFileName;
                System.IO.Directory.CreateDirectory(tmpDirPath);
                Stream stream = new MemoryStream(data);
                File.Delete(tmpFilePath);
                var fileStream = File.Create(tmpFilePath);
                fileStream.Write(data, 0, data.Length);
                fileStream.Close();

                DataTable sheet1 = new DataTable();
                OleDbConnectionStringBuilder csbuilder = new OleDbConnectionStringBuilder();
                csbuilder.Provider = "Microsoft.ACE.OLEDB.12.0";
                csbuilder.DataSource = @"C:\LIMS_Tools\Alcor\Temp\tmp.xls";
                //csbuilder.Add("Extended Properties", "Excel 12.0 Xml;IMEX=1;HDR=No;ImportMixedTypes=Text;");
                //csbuilder.Add("Extended Properties", "Excel 12.0 Xml;IMEX=1;HDR=No");
                csbuilder.Add("Extended Properties", "Excel 12.0 Xml;IMEX=1;ImportMixedTypes=Text;");
                string buf = "";
                string [] wSheets = this.Parameters["worksheet"]?.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                using (OleDbConnection connection = new OleDbConnection(csbuilder.ConnectionString))
                {
                    connection.Open();

                    foreach (string wSheet in wSheets)
                    {
                        try
                        {
                            string selectSql = string.Format(@"SELECT * FROM [{0}]", wSheet);
                            Log(selectSql);


                            using (OleDbDataAdapter adapter = new OleDbDataAdapter(selectSql, connection))
                            {
                                buf += wSheet + "\r\n";
                                adapter.Fill(sheet1);
                                foreach (DataColumn c in sheet1.Columns)
                                {
                                    string s = c.ToString();
                                    //c.DataType = typeof(string);
                                    //if (!string.IsNullOrEmpty(s))
                                    buf += $"{c};";
                                }
                                buf += "\r\n";
                                foreach (DataRow r in sheet1.Rows)
                                {
                                    foreach (var cell in r.ItemArray)
                                    {
                                        string s = cell.ToString();
                                        //if (!string.IsNullOrEmpty(s))
                                        buf += $"{cell};";
                                    }
                                    buf += "\r\n";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log(ex.Message);
                            Log(ex.StackTrace);
                        }
                    }
                    connection.Close();
                }

                /*
                 *                IExcelDataReader excelReader = ExcelReaderFactory.CreateBinaryReader(stream, ReadOption.Loose);
                                object o = excelReader.NextResult();

                                //excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream);
                                DataSet result = excelReader.AsDataSet();
                                string buf = "";
                                foreach (DataRow row in result.Tables[0].Rows)
                                {
                                    foreach (object cell in row.ItemArray)
                                    {
                                        string s = cell.ToString();
                                        if (!string.IsNullOrEmpty(s))
                                            buf += $"{cell};";
                                    }
                                    buf += "\r\n";
                                }

                                excelReader.Close();
                                */
                Results.Clear();
                Results["Check"] = buf;
                DataReady(Results);
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Log(ex.StackTrace);
                Log("Вывод с устройства не распознан");
            }
        }
    }
}
