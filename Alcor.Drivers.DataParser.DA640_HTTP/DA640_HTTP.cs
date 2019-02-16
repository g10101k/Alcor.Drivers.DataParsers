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
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Indusoft.Alcor.Drivers.DataParser.GenericParser;
using System.Globalization;


namespace Indusoft.Alcor.Drivers.DataParser.DA640_HTTP
{
    [Export(typeof(IGenericParser))]
    public class DA640_HTTP : BaseParser
    {
        Dictionary<string, object> Results = new Dictionary<string, object>();
        private static object objLock = new object();
        /// <summary>
        /// Инструкции для выполнение в дочернем потоке.
        /// </summary>
        Action<object> action = (object obj) => {
            //lock (objLock)
            {
                DA640_HTTP self = null;                         // Переменная для дочернего экземпляра парсера
                int SleepTime = 5000;                           // Время между обращениями
                string Url = "";                                // Url запрос
                string DateTimeFormat = "";
                DateTime StartMeasDate = DateTime.MinValue;     // Время последнего определения на приборе, более ранние определения не обрабатываются
                                                                // если параметр не задан будут обработаны все по
                while (true)                                    // Запускаем бесконечный цикл
                {
                    try
                    {
                        if (obj != null)                            // Проверям проинициализировался ли дочерний объект(ВОЗМОЖНО ЛИШНЕЕ)
                        {
                            self = (DA640_HTTP)obj;                 // Прис ваеваем переменной полученный параметр задачи 
                            if (self.Parameters != null)            // Проверям проинициализировался ли обхект с параметрами парсера                    
                            {
                                // Получаем параметры парсера из файла Congig.xml
                                SleepTime = self.Parameters.ContainsKey("sleeptime") ? Convert.ToInt32(self.Parameters["sleeptime"]) : SleepTime;
                                Url = self.Parameters.ContainsKey("url") ? self.Parameters["url"] : "";                            
                                DateTimeFormat = (self.Parameters.ContainsKey("datetimeformat")) ? self.Parameters["datetimeformat"] : "dd.MM.yyyy HH:mm";                                
                                StartMeasDate = (self.Parameters.ContainsKey("startmeasdate") && StartMeasDate == DateTime.MinValue && DateTime.TryParseExact(self.Parameters["startmeasdate"], DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out StartMeasDate)) ? StartMeasDate : StartMeasDate;
                            }
                            if (!string.IsNullOrEmpty(Url)) // Проверяем наличие сслыки можно использовать ссылки типа file:///
                            {
                                try
                                {
                                    // Создаем и настраиваем веб-клиент
                                    WebClient client = new WebClient();
                                    client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                                    // Окрываем ссылку и получаем данные в поток:
                                    Stream data = client.OpenRead(Url);
                                    StreamReader reader = new StreamReader(data);
                                    // Читаем данные в строку:
                                    string s = reader.ReadToEnd();

                                    // Делим полученый CSV файл на строки:
                                    string[] buf = s.Split(new string[] { "\n" }, StringSplitOptions.None);
                                    List<Dictionary<string, string>> array = new List<Dictionary<string, string>>();
                                    // Создаем массив заголовков и добавлем заголовок USER (почему в файле пропущен не знаю...)
                                    string[] headers = buf[0].Split(new string[] { "," }, StringSplitOptions.None);
                                    Array.Resize<string>(ref headers, headers.Length + 1);
                                    headers[headers.Length - 1] = "User";

                                    // Заполняем лист словарей:
                                    for (int i = 0, end = buf.Length - 1; i < end; i++)
                                    {
                                        if (i != 0)
                                        {
                                            string[] values = buf[i].Split(new string[] { "," }, StringSplitOptions.None);
                                            array.Add(new Dictionary<string, string>());
                                            for (int j = 0, end2 = headers.Length - 1; j < end2; j++)
                                            {
                                                array.Last<Dictionary<string, string>>()[headers[j].Replace(" ", "").Replace(".", "").Replace("(", "").Replace(")", "")] = values[j];
                                            }
                                        }
                                    }
                                    int errorlevel = 0;
                                    // Начинаем обрабатывать данные начиная с более позних записей.
                                    for (int z = array.Count - 1; z >= 0; z--)
                                    {
                                        try
                                        {                                        
                                            Dictionary<string, string> record = array[z];
                                            // Преобразовываем дату из японского стиля
                                            DateTime MeasDate;
                                            DateTime.TryParse(record["MeasDate"], out MeasDate);
                                            // Сохраняем дату в стиле дд.ММ.гггг чч:мм
                                            record["MeasDate"] = MeasDate.ToString();
                                            // Проверяем дату определения если старше начальной даты то обрабатываем
                                            if (MeasDate > StartMeasDate)
                                            {
                                                // Устанавливаем новую начальную дату
                                                StartMeasDate = MeasDate;
                                                string Check = "";
                                                foreach (KeyValuePair<string, string> pair in record)
                                                {
                                                    //if (!string.IsNullOrEmpty(pair.Value))
                                                    {
                                                        Check += (pair.Key == "Density") ? $"{pair.Key}{record["MeasTemp"].Replace("00", "").Replace(".", "")};{pair.Value}\r\n" : $"{pair.Key};{pair.Value}\r\n";
                                                        self.Results[pair.Key] = pair.Value;
                                                    }
                                                }
                                                //self.Log($"Новая проба от: {MeasDate}");

                                                self.Results["Check"] = Check;
                                                self.DataReady(self.Results);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            errorlevel++;
                                            self.Log($"{ex.Message}\r\n {ex.StackTrace}");
                                        }
                                    }

                                    if (errorlevel == 0 && array.Count != 0) //Если нет ошибок и прочитаны пробы
                                        client.OpenRead(Url.Replace("result.csv", "DataClear.cgi"));
                                }
                                catch (Exception ex)
                                {
                                    if (obj != null)
                                    {
                                        self.Log($"{ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        self.Log($"{ex.Message} \r\n {ex.StackTrace}");
                    }
                    Thread.Sleep(SleepTime);
                }
            }
        };


        /// <summary>
        /// Конструктор драйвера, выполняется единажды при инициализации драйвера
        /// </summary>
        public DA640_HTTP()
        {


        }

        public override void Initialize()
        {
            //this.Log(AppDomain.CurrentDomain.BaseDirectory);
            //FileStream fs = File.Open("Indusoft.Alcor.Drivers.DataParser.Flow.template", FileMode.Open);
            //lock (objLock)
            {
                //this.Log(this.Parameters?.ToString()); 
                Task task = new Task(action, this, TaskCreationOptions.AttachedToParent);
                task.Start();
            }
        }

        /// <summary>
        /// Возвращает имя драйвера
        /// </summary>
        public override string Name
        {
            get
            {
                return "DA640_HTTP";
            }
        }
        /// <summary>
        /// Разбирает данные полученные Алькором из канала данных
        /// </summary>
        /// <param name="data">Массив байтов полученых из канала данных</param>
        public override void Parse(byte[] data)
        {

        }

    }
}
