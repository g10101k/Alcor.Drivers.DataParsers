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
using System.Globalization;
using ELW.Library.Math;
using ELW.Library.Math.Exceptions;
using ELW.Library.Math.Expressions;
using ELW.Library.Math.Tools;

namespace Indusoft.Alcor.Drivers.DataParser.RegularExpressionParser
{
    [Export(typeof(IGenericParser))]
    public class RegularExpressionParser : BaseParser
    {
        Dictionary<string, object> Results = new Dictionary<string, object>();
        Dictionary<string, string> Params = new Dictionary<string, string>();
        Dictionary<string, gParamRegExp> Config = new Dictionary<string, gParamRegExp>();
        Dictionary<string, gParamRegExp> Head = new Dictionary<string, gParamRegExp>();
        Dictionary<string, string> HeadValue = new Dictionary<string, string>();
        Dictionary<string, gParamRegExp> Group = new Dictionary<string, gParamRegExp>();
        Dictionary<string, string> GroupValue = new Dictionary<string, string>();
        Dictionary<string, gParamRegExp> Calc = new Dictionary<string, gParamRegExp>();
        Dictionary<string, string> CalcValue = new Dictionary<string, string>();
        string Buffer = "";

        /// <summary>
        /// Конструктор драйвера, выполняется единажды при инициализации драйвера
        /// </summary>
        public RegularExpressionParser()
        {

        }
        /// <summary>
        /// Возвращает имя драйвера
        /// </summary>
        public override string Name
        {
            get
            {
                return "RegularExpressionParser";
            }
        }

        public override void Initialize()
        {

        }

        /// <summary>
        /// Разбирает данные полученные Алькором из канала данных
        /// </summary>
        /// <param name="data">Массив байтов полученых из канала данных</param>
        public override void Parse(byte[] data)
        {
            try
            {
                Results.Clear();
                try
                {
                    if (this.Parameters != null)
                    {
                        // Теряем скорость но может сработать загрузка конфигурации.
                        Config.Clear();
                        Head.Clear();
                        HeadValue.Clear();
                        Group.Clear();
                        GroupValue.Clear();

                        foreach (KeyValuePair<string, string> pair in this.Parameters)
                        {
                            try
                            {
                                if (pair.Key.ToLower().Contains("config"))
                                {
                                    gParamRegExp.getParamRegExp(Config, pair);
                                }
                                else if (pair.Key.ToLower().Contains("head"))
                                {
                                    gParamRegExp.getParamRegExp(Head, pair);
                                }
                                else if (pair.Key.ToLower().Contains("group"))
                                {
                                    gParamRegExp.getParamRegExp(Group, pair);
                                }
                                else if (pair.Key.ToLower().Contains("calc"))
                                {
                                    gParamRegExp.getParamRegExp(Calc, pair);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"[ERROR] Ошибка при разборе параметров");
                                Log($"[ERROR] {ex.Message}");
                                Log($"[ERROR] {ex.StackTrace}");
                            }
                        }
                    }
                    string Message = "";
                    try
                    {
                        Message = GetMessageInCodePage(data);
                    }
                    catch (Exception ex)
                    {
                        Log($"Неудалось получить данный в правильной кодировке");
                        Log($"{ex.Message}");
                        Log($"{ex.StackTrace}");
                        return;
                    }

                    // Удаляем из сообщения не нужные строки
                    if (Config.ContainsKey("deletefromdata"))
                    {
                        MatchCollection match = Regex.Matches(Message, Config["deletefromdata"].Value, Config["deletefromdata"].RegOpt);
                        for (int i = match.Count - 1; i >= 0; i--)
                        {
                            Message = Message.Remove(match[i].Index, match[i].Length);
                        }
                    }

                    if (Config.ContainsKey("concatwhilenotread"))
                    {
                        try
                        {
                            Buffer += Message;

                            Match match = GetMatch(Buffer, Config["concatwhilenotread"]);
                            if (match != null)
                            {
                                // Нашли вхождение начинаем обрабатывать полученный файл
                                Message = Buffer;
                                Buffer = "";
                            }
                            else
                            {
                                // Не нашли вхождение - добавляем в буфер, выходим из функции
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[ERROR] Не удалось найти начало чтения {Config["startregexp"].Value}");
                            Log($"[ERROR] {ex.Message}");
                            Log($"[ERROR] {ex.StackTrace}");
                        }
                    }

                    if (Config.ContainsKey("startread"))
                    {
                        try
                        {
                            Match match = GetMatch(Message, Config["startread"]);
                            if (match != null)
                            {
                                Message = Message.Remove(0, match.Index + match.Length);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[ERROR] Не удалось найти начало чтения {Config["startread"].Value}");
                            Log($"[ERROR] {ex.Message}");
                            Log($"[ERROR] {ex.StackTrace}");
                        }
                    }
                    if (Config.ContainsKey("endread"))
                    {
                        try
                        {
                            Match match = GetMatch(Message, Config["endread"]);
                            if (match != null)
                            {
                                Message = Message.Remove(match.Index);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[ERROR] Не удалось найти начало чтения {Config["endread"].Value}");
                            Log($"[ERROR] {ex.Message}");
                            Log($"[ERROR] {ex.StackTrace}");
                        }
                    }

                    string head = "", body = "";
                    try
                    {
                        MatchCollection match = Regex.Matches(Message, Config["headmask"].Value, Config["headmask"].RegOpt);
                        if (match.Count > 0)
                        {
                            head = match[0].Value;
                            body = Message.Replace(head, "");
                        }
                        else
                            throw new Exception();
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Неудалось выделить заголовки");
                        Log($"[ERROR] {ex.Message}");
                        Log($"[ERROR] {ex.StackTrace}");
                        head = Message;
                        body = Message;
                    }

                    // Получаем значения общие для всех проб
                    try
                    {
                        foreach (KeyValuePair<string, gParamRegExp> pair in Head)
                        {
                            Match match = GetMatch(Message, pair.Value);
                            if (match != null)
                            {
                                HeadValue.Add(pair.Key, GetGroupgs(match));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Не удалось получить общие значения для всех проб");
                        Log($"[ERROR] {ex.Message}");
                        Log($"[ERROR] {ex.StackTrace}");
                    }


                    try
                    {
                        if (!Config.ContainsKey("groupmask")) {
                            Config.Add("groupmask", new gParamRegExp("^.*$", "0", "531"));
                        }
                        MatchCollection MatchGroups = Regex.Matches(body, Config["groupmask"].Value, Config["groupmask"].RegOpt);
                        Log($"Найдено {MatchGroups.Count} вхождений выражения config:groupmask :\"{Config["groupmask"].Value}\"");
                        if (MatchGroups.Count == 0)
                            Log(Message);
                        for (int i = 0, e = MatchGroups.Count; i < e; i++)
                        {
                            if (MatchGroups[i].Value.Length > 0)
                            {
                                GroupValue.Clear();
                                foreach (KeyValuePair<string, gParamRegExp> pair in Group)
                                {
                                    Match match = GetMatch(MatchGroups[i].Value, pair.Value);
                                    if (match != null)
                                    {
                                        GroupValue.Add(pair.Key, GetGroupgs(match));
                                    }
                                }

                                // нужно посчитать все calc
                                foreach (KeyValuePair<string, gParamRegExp> pair in Calc)
                                {
                                    Double res = CalcExp(pair.Value.Value);
                                    if (!Double.IsNaN(res))
                                    {
                                        GroupValue.Add(pair.Key, res.ToString());
                                    }
                                }

                                foreach (KeyValuePair<string, List<string>> pair in this.Unions_Tags)
                                {
                                    foreach (string key in pair.Value)
                                        Results[key] = "";
                                }

                                foreach (KeyValuePair<string, string> pair in HeadValue)
                                {
                                    Results[pair.Key] = pair.Value;
                                    Results["Check"] += $"{pair.Key};{pair.Value}\r\n";
                                }

                                foreach (KeyValuePair<string, string> pair in GroupValue)
                                {
                                    Results[pair.Key] = pair.Value;
                                    Results["Check"] += $"{pair.Key};{pair.Value}\r\n";
                                }
                                Results["Index"] = i.ToString();

                                DataReady(Results);
                                Results.Clear();

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Неудалось получить заголовки");
                        Log($"[ERROR] {ex.Message}");
                        Log($"[ERROR] {ex.StackTrace}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Что то пошло не так");
                    Log($"[ERROR] {ex.Message}");
                    Log($"[ERROR] {ex.StackTrace}");
                }
            }
            catch
            {
                Log("[ERROR] Вывод с устройства не распознан");
            }
        }
        private string GetGroupgs(Match m)
        {
            string s = "";
            if (m.Groups.Count == 2)
            {
                s += m.Groups[1].Value;
            }
            else if (m.Groups.Count > 2)
            {
                for (int i = 1, c = m.Groups.Count; i < c; i++)
                {
                    s += m.Groups[i].Value + ";";
                }
            }
            return s;
        }
        private string GetMessageInCodePage(byte[] data)
        {
            string CodePage = (Config.ContainsKey("codepage")) ? Config["codepage"].Value : "default";
            try
            {
                if (CodePage.ToLower() == "default")
                {
                    return Encoding.Default.GetString(data);
                }
                else if (CodePage.ToLower() == "utf8")
                {
                    return Encoding.UTF8.GetString(data);
                }
                else if (CodePage.ToLower() == "utf7")
                {
                    return Encoding.UTF7.GetString(data);
                }
                else if (CodePage.ToLower() == "utf32")
                {
                    return Encoding.UTF32.GetString(data);
                }
                else if (CodePage.ToLower() == "unicode")
                {
                    return Encoding.Unicode.GetString(data);
                }
                else if (CodePage.ToLower() == "bigendianunicode")
                {
                    return Encoding.BigEndianUnicode.GetString(data);
                }
                else
                {
                    try
                    {
                        return Encoding.GetEncoding(Convert.ToInt32(CodePage)).GetString(data);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            catch (Exception)
            {
                Log($"Кодировка \"{CodePage}\" в параметре не распознана Config:CodePage");
            }
            return "";
        }

        private Match GetMatch(string message, gParamRegExp prm)
        {
            RegexOptions options = prm.RegOpt;
            Regex regexp = new Regex(prm.Value, options);
            MatchCollection math = regexp.Matches(message);
            Log($"Найдено {math.Count} вхождений выражения : {prm.Value}");
            if (math.Count > 0)
            {
                if (prm.Index < 0)
                {
                    int ind = math.Count + prm.Index;
                    if (ind >= 0)
                    {
                        Log($"Выбрано вхождение с индексом {ind} ");
                        return math[ind];
                    }
                    else
                    {
                        Log($"Не выбрано вхождение с индексом  {ind} ");
                        return null;
                    }
                }
                else
                {
                    if (prm.Index < math.Count)
                    {
                        Log($"Выбрано вхождение с индексом {prm.Index} ");
                        return math[prm.Index];
                    }
                    else
                    {
                        Log($"Не выбрано вхождение с индексом {prm.Index} ");
                        return null;
                    }

                }
            }
            return null;
        }

        private Double CalcExp(string exp)
        {
            try
            {
                exp = exp.Replace(" ", "").Replace("\r", "").Replace("\n", "");
                // Compiling an expression
                PreparedExpression preparedExpression = ToolsHelper.Parser.Parse(exp);
                CompiledExpression compiledExpression = ToolsHelper.Compiler.Compile(preparedExpression);
                CompiledExpression optimizedExpression = ToolsHelper.Optimizer.Optimize(compiledExpression);
                // Creating list of variables specified
                List<VariableValue> variables = new List<VariableValue>();


                foreach (KeyValuePair<string, string> pair in GroupValue)
                {
                    double val;
                    string var = pair.Value.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator).Replace(",", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);
                    if (!Double.TryParse(var, out val))
                        Log($"{pair.Key} = {var} - не число");
                    else
                        variables.Add(new VariableValue(val, pair.Key));
                }
                return ToolsHelper.Calculator.Calculate(compiledExpression, variables);
            }
            catch (CompilerSyntaxException ex)
            {
                Log(String.Format("Compiler syntax error: {0}", ex.Message));
            }
            catch (MathProcessorException ex)
            {
                Log(String.Format("Error: {0}", ex.Message));
            }
            catch (ArgumentException)
            {
                Log("Error in input data.");
            }
            catch (Exception)
            {
                Log("Unexpected exception.");
            }

            return Double.NaN;
        }

        class gParamRegExp
        {
            public string Value;
            public int Index;
            public RegexOptions RegOpt;
            public gParamRegExp(string _regExp, string _index, string _regopt)
            {
                Value = _regExp;
                Index = Convert.ToInt32(_index);
                RegOpt = (RegexOptions)Convert.ToInt32(_regopt);
            }
            public static void getParamRegExp(Dictionary<string, gParamRegExp> dic, KeyValuePair<string, string> pair)
            {
                char[] spl = new char[] { ':' };
                string[] buf = pair.Key.Split(spl);
                string index = (buf.Length >= 3) ? buf[2] : "0";
                string regopt = (buf.Length >= 4) ? buf[3] : "531";

                dic.Add(buf[1], new gParamRegExp(pair.Value, index, regopt));
            }

            public override string ToString()
            {
                return $"[{Index}]:{Value}";
            }
        }
    }

}
