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

namespace Indusoft.Alcor.Drivers.DataParser.GetHTTP
{
    [Export(typeof(IGenericParser))]
    public class GetHTTP : BaseParser
    {
        public override string Name
        {
            get
            {
                return "GetHTTP";
            }
        }

        Dictionary<string, object> Results = new Dictionary<string, object>();
        private static object objLock = new object();
        /// <summary>
        /// Инструкции для выполнение в дочернем потоке.
        /// </summary>
        Action<object> action = (object obj) =>
        {
            //lock (objLock)
            {
                GetHTTP self = null;                         // Переменная для дочернего экземпляра парсера
                int SleepTime = 5000;                           // Время между обращениями
                string Url = "";                                // Url запрос
                //string DateTimeFormat = "";
                //DateTime StartMeasDate = DateTime.MinValue;     // Время последнего определения на приборе, более ранние определения не обрабатываются
                                                                // если параметр не задан будут обработаны все по
                while (true)                                    // Запускаем бесконечный цикл
                {
                    try
                    {
                        if (obj != null)                            // Проверям проинициализировался ли дочерний объект(ВОЗМОЖНО ЛИШНЕЕ)
                        {
                            self = (GetHTTP)obj;                 // Прис ваеваем переменной полученный параметр задачи 
                            if (self.Parameters != null)            // Проверям проинициализировался ли обхект с параметрами парсера                    
                            {
                                // Получаем параметры парсера из файла Congig.xml
                                SleepTime = self.Parameters.ContainsKey("sleeptime") ? Convert.ToInt32(self.Parameters["sleeptime"]) : SleepTime;
                                Url = self.Parameters.ContainsKey("url") ? self.Parameters["url"] : "";
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

                                    self.Results["Check"] = s;
                                    self.DataReady(self.Results);

                                    data.Close();
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

        public override void Parse(byte[] data)
        {

        }
    }
}
