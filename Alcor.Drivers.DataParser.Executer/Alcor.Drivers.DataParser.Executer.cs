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
using System.Diagnostics;

namespace Indusoft.Alcor.Drivers.DataParser.Executer
{
    [Export(typeof(IGenericParser))]
    public class Executer : BaseParser
    {
        public override string Name
        {
            get
            {
                return "Executer";
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
                Executer self = null;                         // Переменная для дочернего экземпляра парсера
                TimeSpan SleepTime = TimeSpan.Parse("30:00:00");
                
                SleepTime.Add(SleepTime);// Время между обращениями
                
                while (true)                                    // Запускаем бесконечный цикл
                {
                    try
                    {
                        if (obj != null)                            // Проверям проинициализировался ли дочерний объект(ВОЗМОЖНО ЛИШНЕЕ)
                        {
                            Dictionary<string, string> cmd = new Dictionary<string, string>();
                            self = (Executer)obj;                 // Прис ваеваем переменной полученный параметр задачи 
                            if (self.Parameters != null)            // Проверям проинициализировался ли обхект с параметрами парсера                    
                            {
                                // Получаем параметры парсера из файла Congig.xml
                                SleepTime = self.Parameters.ContainsKey("sleeptime") ? TimeSpan.Parse(self.Parameters["sleeptime"]) : SleepTime;
                                foreach (KeyValuePair<string, string> pair in self.Parameters)
                                {
                                    if (pair.Key.StartsWith("cmd:")) cmd.Add(pair.Key.Replace("cmd:", ""), pair.Value);
                                }
                            }

                            foreach (KeyValuePair<string, string> pair in cmd)
                            {
                                Process proc = new Process()
                                {
                                    StartInfo = new ProcessStartInfo(pair.Key, pair.Value)
                                    {
                                        RedirectStandardOutput = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        WindowStyle = ProcessWindowStyle.Hidden
                                    }
                                };

                                proc.Start();

                                if (!proc.StartInfo.RedirectStandardOutput)
                                    return;

                                StreamReader sr = proc.StandardOutput;

                                while (!sr.EndOfStream)
                                {
                                    self.Log($" (action) {sr.ReadLine()}");
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
            Task task = new Task(action, this, TaskCreationOptions.AttachedToParent);
            task.Start();
        }

        public override void Parse(byte[] data)
        {

        }
    }
}
