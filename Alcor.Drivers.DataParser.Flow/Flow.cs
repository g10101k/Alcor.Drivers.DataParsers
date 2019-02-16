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

namespace Indusoft.Alcor.Drivers.DataParser.Flow
{
    [Export(typeof(IGenericParser))]
    public class Flow : BaseParser
    {
        Dictionary<string, object> Results = new Dictionary<string, object>();
        /// <summary>
        /// Конструктор драйвера, выполняется единажды при инициализации драйвера
        /// </summary>
        public Flow()
        {

        }
        /// <summary>
        /// Возвращает имя драйвера
        /// </summary>
        public override string Name
        {
            get
            {
                return "Flow";
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
                // Реализвация драйвера
                // Находим нужную кодировку:
                Results["MsgInCodePageDefault"] = Encoding.Default.GetString(data);
                Results["MsgInCodePageUTF8"] = Encoding.UTF8.GetString(data);
                Results["MsgInCodePageUTF7"] = Encoding.UTF7.GetString(data);
                Results["MsgInCodePageUTF32"] = Encoding.UTF32.GetString(data);
                Results["MsgInCodePageUnicode"] = Encoding.Unicode.GetString(data);
                Results["MsgInCodePage866"] = Encoding.GetEncoding(866).GetString(data);
                Results["MsgInCodePage850"] = Encoding.GetEncoding(850).GetString(data);
                Results["MsgInCodePage1251"] = Encoding.GetEncoding(1251).GetString(data);
                // Добавляем теги в выходной массив:
               
                // Передаем гововый пакет данных алькору для вывода в файл:
                DataReady(Results);
            }
            catch
            {
                Log("Вывод с устройства не распознан");
            }
        }
    }
}
