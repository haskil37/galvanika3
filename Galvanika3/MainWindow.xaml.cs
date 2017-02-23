﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;

namespace Galvanika3
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Переменные
        public List<MemoryData> MemoryGridTable = new List<MemoryData>();
        public List<ProgramData> DataGridTable = new List<ProgramData>();
        public List<MyTimers> TimerGridTable = new List<MyTimers>();

        public List<int> InputData = new List<int>() { 125, 126, 173, 2, 0, 0, 0, 0 };
        public List<int> MarkerData = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public List<int> OutputData = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0 };

        public Dictionary<string, string> DB = new Dictionary<string, string>();
        public Dictionary<int, int> StartEnd = new Dictionary<int, int>();

        public Dictionary<string, int> TimerSE = new Dictionary<string, int>();
        public Dictionary<string, int> TimerSA = new Dictionary<string, int>();

        public Dictionary<string, int> FrontP = new Dictionary<string, int>();
        public Dictionary<string, int> FrontN = new Dictionary<string, int>();

        public BackgroundWorker backgroundWorker = new BackgroundWorker();

        public Dictionary<string, int> Stek = new Dictionary<string, int>();
        #endregion
        #region Отправка выражения в парсер
        private bool Parse(string value)
        {
            var tokens = new Tokenizer(value).Tokenize();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
        #endregion
        #region Чтение файла с программой
        private string Path = "0000000d.AWL";
        private List<string> tempDB = new List<string>();
        private List<string> tempProgramList = new List<string>();
        private bool ReadFileDB()
        {
            if (!File.Exists(Path))
                return false;
            using (StreamReader fs = new StreamReader(Path, Encoding.Default))
            {
                int start = 0;
                while (true)
                {
                    string temp = fs.ReadLine();
                    if (temp == null) break;
                    if (temp.Contains("END_STRUCT"))
                        break;

                    if (start == 1)
                        tempDB.Add(temp);

                    if (temp.Contains("STRUCT") && start != 1)
                        start = 1;
                }
                ParseDB();
                start = 0;
                while (true)
                {
                    string temp = fs.ReadLine();
                    if (temp == null) break;
                    if (temp.Contains("FUNCTION FC") && start != 1)
                        start = 1;
                    if (start == 1 && !temp.Contains("NOP"))
                        tempProgramList.Add(temp);
                    if (temp.Contains(": NOP"))
                        tempProgramList.Add(temp);
                }
            }
            FillGrid();
            return true;
        }
        private void ParseDB()
        {
            foreach (var item in tempDB)
            {
                if (string.IsNullOrEmpty(item))
                    break;
                var itemNew = item;
                if (item.Contains("//"))
                    itemNew = item.Substring(0, item.IndexOf('/'));
                var tempFirstString = itemNew.Split('_');
                var tempSecondString = tempFirstString[1].Split('i');
                string tempIndex = "";
                if (tempSecondString.Count() > 1)
                    tempIndex = tempSecondString[0] + "." + tempSecondString[1];
                else
                    tempIndex = tempSecondString[0];
                var tempThirdString = tempFirstString[tempFirstString.Count() - 1].Split('=');
                if (tempThirdString.Count() > 1)
                {
                    var endOfString = tempThirdString[1].Trim();

                    if (endOfString.Contains(';'))
                        endOfString = endOfString.Remove(endOfString.Length - 1, 1);

                    DB.Add(tempIndex.Trim(), endOfString);
                }
                else
                {
                    if (tempThirdString[0].Contains("BOOL"))
                        DB.Add(tempIndex, "False");
                    else
                        DB.Add(tempIndex, "0");
                }
                var tempString = itemNew.Substring(itemNew.IndexOf('_') + 1); //Дважды удаляем до знака "_"
                tempString = tempString.Substring(tempString.IndexOf('_') + 1);
                var tempNameP = tempString.Split(':');
                MemoryData result = new MemoryData("", "", "", "", "");
                if (tempNameP.Count() > 2)
                {
                    var value = tempNameP[2].Replace('=', ' ');
                    value = value.Replace(';', ' ');
                    if (tempNameP[1].Contains("BOOL"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "bool", value.Trim().ToLower(), value.Trim().ToLower());
                    if (tempNameP[1].Contains("INT"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "integer", value.Trim(), value.Trim());
                    if (tempNameP[1].Contains("TIME"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "timer", value.Trim(), value.Trim());
                    if (tempNameP[0].Contains("Stek") && tempNameP[0].Trim() != "Stek2" && tempNameP[0].Trim() != "Stek1")
                    {
                        var tempTimerData = value.ToLower().Split('#');
                        string tempTime;
                        int newTempTime;
                        if (tempTimerData[1].Contains("ms"))
                        {
                            tempTime = tempTimerData[1].Replace("ms", "");
                            newTempTime = Convert.ToInt32(tempTime);
                        }
                        else
                        {
                            tempTime = tempTimerData[1].Replace("s", "");
                            newTempTime = Convert.ToInt32(tempTime);
                            newTempTime = newTempTime * 1000;
                        }
                        Stek.Add(tempNameP[0].Trim(), newTempTime);
                    }
                }
                else
                {
                    if (tempNameP[1].Contains("BOOL"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "bool", "false", "false");
                    if (tempNameP[1].Contains("INT"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "integer", "0", "0");
                    if (tempNameP[1].Contains("TIME"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "timer", "0", "0");
                }
                MemoryGridTable.Add(result);
            }
        }
        private void FillGrid()
        {
            var countKey = 0;
            var countText = 0;
            foreach (string item in tempProgramList) // Загоняем в таблицу данные программы из файла
            {
                try
                {
                    if (item.Contains("NETWORK") || item.Contains("TITLE") || item.Contains("END") || item.Contains("FUNCTION FC") || item.Contains("VERSION") || item.Contains("BEGIN") || item.Contains("AUF   DB"))
                    {
                        var result = new ProgramData(0, item, "", "", "", "", "", "");
                        DataGridTable.Add(result);
                        if (StartEnd.Count != 0) //Разбитие на подпрограммы
                        {
                            var lastStart = StartEnd.Last();
                            if (lastStart.Key == lastStart.Value)
                                StartEnd[lastStart.Key] = countKey + countText - 1;
                        }
                        countText++;
                    }
                    else if (item.Trim().Length != 0)
                    {
                        if (StartEnd.Count != 0) //Разбитие на подпрограммы
                        {
                            var lastStart = StartEnd.Last();
                            if (lastStart.Key != lastStart.Value)
                                StartEnd.Add(countKey + countText, countKey + countText);
                        }
                        else
                            StartEnd.Add(countText, countText);

                        var itemSplit = item.Replace(';', ' ');
                        var stringData = itemSplit.Split(' ').ToList();
                        stringData.RemoveAll(RemoveEmpty);
                        countKey++;
                        if (stringData.Count > 2)
                        {
                            var result = new ProgramData(countKey, item, stringData[0], stringData[1], stringData[2], "", "", "");
                            DataGridTable.Add(result);
                            if (stringData.Contains("FP"))
                                FrontP.Add(countKey.ToString(), 0);
                            if (stringData.Contains("FN"))
                                FrontN.Add(countKey.ToString(), 0);
                        }
                        else if (stringData.Count == 2)
                        {
                            if (stringData.Contains("SPBNB"))
                            {
                                var result = new ProgramData(countKey, item, stringData[0], stringData[1], "", "", "", "");
                                DataGridTable.Add(result);
                            }
                            else if (stringData.Contains("S5T"))
                            {
                                var stringTimer = stringData[1].Split('#');
                                var result = new ProgramData(countKey, item, stringData[0], stringTimer[0], stringTimer[1], "", "", "");
                                DataGridTable.Add(result);
                            }
                            else if (stringData.Contains("L"))
                            {
                                var result = new ProgramData(countKey, item, stringData[0], stringData[1], "", "", "", "");
                                DataGridTable.Add(result);
                            }
                        }
                        else
                        {
                            var result = new ProgramData(countKey, item, stringData[0], "", "", "", "", "");
                            DataGridTable.Add(result);
                        }
                    }
                }
                catch
                {
                }
            }
        }
        private bool RemoveEmpty(String s)
        {
            return s.Length == 0;
        }
        #endregion
        #region Таймер и поток расчета
        private void timer_Tick(object sender, EventArgs e)
        {
            try
            {
                foreach (var item in TimerSE)
                {
                    var value = TimerGridTable.Where(u => u.Address == item.Key).SingleOrDefault();
                    if (value.Time < value.EndTime && value.Value != 1)
                        value.Time += 100;
                    else
                    {
                        value.Value = 1;
                        value.Time = 0;
                    }
                }
                foreach (var item in TimerSA)
                {
                    var value = TimerGridTable.Where(u => u.Address == item.Key).SingleOrDefault();
                    if (value.Time < value.EndTime && value.Time != -1 && value.Value != 0)
                        value.Time += 100;
                    else if (value.Time == value.EndTime)
                    {

                        value.Value = 0;
                        value.Time = 0;
                        TimerSA.Remove(item.Key);
                    }
                }
            }
            catch
            {
                return;
            }
        }
        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Calculate();
        }
        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            backgroundWorker.RunWorkerAsync();
        }
        #endregion
        public MainWindow()
        {
            InitializeComponent();
            button_Start.Focus();
            var openFile = ReadFileDB();
            if (!openFile)
                return;

            FillTextBoxes();

            backgroundWorker.DoWork += backgroundWorker_DoWork;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
            backgroundWorker.WorkerSupportsCancellation = true;

            DispatcherTimer timerForTimer = new DispatcherTimer();
            timerForTimer.Tick += new EventHandler(timer_Tick);
            timerForTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timerForTimer.Start();

            DispatcherTimer timerForInput = new DispatcherTimer();
            timerForInput.Tick += new EventHandler(timer_Tick_Input);
            timerForInput.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timerForInput.Start();

        }
        #region Расчет
        private void Calculate()
        {
            foreach (var item in StartEnd)
            {
                string output = "";
                string doubleBKT = ""; //переменная для двойной закрывающей скобки
                var compareValues = new List<int>();
                for (int i = item.Key; i <= item.Value; i++)
                {
                    ProgramData value = DataGridTable[i] as ProgramData;
                    if (value == null)
                        break;

                    if (value.Operator.Contains("=") && !value.Operator.Contains("I")) //Вывод
                    {
                        if (!string.IsNullOrEmpty(doubleBKT))
                        {
                            output += doubleBKT;
                            doubleBKT = "";
                        }

                        if (string.IsNullOrEmpty(output))
                            output = "false";

                        DataWrite(value, output);
                    }
                    else //Cчитываем дальше
                    {
                        string thisOperator = "";
                        if (value.Operator.Contains(")"))
                        {
                            if (output.Trim().LastOrDefault() != '(')
                            {
                                output += ") " + doubleBKT;
                                doubleBKT = "";
                            }
                            else
                            {
                                var temp = output.Trim();
                                temp = temp.Remove(temp.Length - 1, 1);
                                temp = temp.Trim();

                                temp = temp.Substring(0, temp.LastIndexOf(' '));
                                output = temp;
                            }
                        }
                        else if (value.Operator.Contains("U"))
                        {
                            thisOperator = " and ";
                            if (value.Operator.Contains("("))
                                thisOperator += " ( ";
                        }
                        else if (value.Operator.Contains("O"))
                        {
                            thisOperator = " or ";
                            if (value.Operator.Contains("("))
                                thisOperator += " ( ";
                            if (string.IsNullOrEmpty(value.Bit))
                            {
                                thisOperator += " ( ";
                                doubleBKT += ")";
                            }
                        }

                        if (output.Length != 0)
                        {
                            if (output.TrimEnd().LastOrDefault() != '(')
                                output += thisOperator;
                            else
                            {
                                if (thisOperator.TrimEnd().LastOrDefault() == '(')
                                    output += "(";
                            }
                        }
                        else
                        {
                            if (thisOperator.Contains("("))
                                output += " ( ";
                        }

                        if (value.Operator.Contains("L"))
                        {
                            var timerData = ValueBool(value);
                            var timerFromDB = 0;
                            //Проверим на таймер в бд
                            if (value.AEM.Contains("DB") && DB.ContainsKey(value.Bit))
                            {
                                timerData = DB[value.Bit].ToLower();
                                if (timerData.Contains("s5t"))//То это таймер, иначе число
                                    timerFromDB = 1;
                            }
                            if (value.AEM.Contains("S5T") || timerData.Contains("s5t") || timerFromDB == 1) //Если таймер
                            {
                                if (timerData == "0")
                                    timerData = value.AEM.ToLower();
                                var temp = Parse(output);
                                ProgramData valueNext = DataGridTable[i + 1] as ProgramData;
                                if (valueNext.Operator.Contains("SE"))
                                {
                                    i = i + 1;

                                    if (temp == false) //Обнуляем таймер если SE 
                                    {
                                        if (TimerSE.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            TimerSE.Remove(valueNext.Bit.ToString());
                                            MyTimers valueTime = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault() as MyTimers;
                                            valueTime.Time = 0;
                                            valueTime.Value = 0;
                                        }
                                    }
                                    else //Создаем новый таймер если SE 
                                    {
                                        if (!TimerSE.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            var tempTimerData = timerData.Split('#');
                                            string tempTime;
                                            int newTempTime;
                                            if (tempTimerData[1].Contains("ms"))
                                            {
                                                tempTime = tempTimerData[1].Replace("ms", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                            }
                                            else
                                            {
                                                tempTime = tempTimerData[1].Replace("s", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                                newTempTime = newTempTime * 1000;
                                            }
                                            TimerSE.Add(valueNext.Bit, newTempTime);
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer == null)
                                            {
                                                MyTimers valueTime = new MyTimers(valueNext.Bit, 0, newTempTime, 0);
                                                TimerGridTable.Add(valueTime);
                                            }
                                            else
                                            {
                                                containsTimer.EndTime = newTempTime;
                                                containsTimer.Time = 0;
                                                containsTimer.Value = 0;
                                            }
                                        }
                                    }
                                }
                                else if (valueNext.Operator.Contains("SA")) //если SA то наоборот запускаем
                                {
                                    i = i + 1;

                                    if (temp == true) //Обнуляем таймер если SА тут обнуление это 1
                                    {
                                        if (!TimerSA.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            var tempTimerData = timerData.Split('#');
                                            string tempTime;
                                            int newTempTime;
                                            if (tempTimerData[1].Contains("ms"))
                                            {
                                                tempTime = tempTimerData[1].Replace("ms", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                            }
                                            else
                                            {
                                                tempTime = tempTimerData[1].Replace("s", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                                newTempTime = newTempTime * 1000;
                                            }
                                            TimerSA.Add(valueNext.Bit, newTempTime);
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer == null)
                                            {
                                                MyTimers valueTime = new MyTimers(valueNext.Bit, -1, newTempTime, 1);
                                                TimerGridTable.Add(valueTime);
                                            }
                                            else
                                            {
                                                containsTimer.EndTime = newTempTime;
                                                containsTimer.Time = -1;
                                                containsTimer.Value = 1;
                                            }
                                        }
                                        else
                                        {
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer != null)
                                            {
                                                containsTimer.Time = -1;
                                                containsTimer.Value = 1;
                                            }

                                        }
                                    }
                                    else
                                    {
                                        if (TimerSA.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer.Time == -1)
                                                containsTimer.Time = 0;//Т.к. SA запускается если с 1 стало 0, то мы из таймеров SA должны скопировать в таймеры SE, и потом проверять когда кончится время, то удалить из таймеров SE 
                                        }
                                    }
                                }
                            }
                            else if (compareValues.Count == 0) //Если сравнение
                            {
                                var temp = output.Trim();
                                temp = temp.Remove(temp.Length - 1, 1);
                                temp = temp.Trim();
                                if (!string.IsNullOrEmpty(temp))
                                {
                                    temp = temp.Substring(0, temp.LastIndexOf(' '));

                                    var tempValue = Parse(temp);
                                    if (tempValue == false)
                                        i = i + 2;
                                }
                            }
                            var currentInt = ValueBool(value);
                            if (!currentInt.ToLower().Contains("s5t"))
                                compareValues.Add(Convert.ToInt32(currentInt));
                        }

                        if (value.Operator.Contains("=="))
                        {
                            if (compareValues[0] == compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains("<>"))
                        {
                            if (compareValues[0] != compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains("<"))
                        {
                            if (compareValues[0] < compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains(">"))
                        {
                            if (compareValues[0] > compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains("+"))
                        {
                            var temp = compareValues[0] + compareValues[1];
                            compareValues[0] = temp;
                        }
                        else if (value.Operator.Contains("-"))
                        {
                            var temp = compareValues[0] - compareValues[1];
                            compareValues[0] = temp;
                        }
                        if (value.Operator.Contains("T"))
                        {
                            DB[value.Bit] = compareValues[0].ToString();
                        }

                        if (value.Operator.Contains("SPBNB")) //Типа goto
                        {
                            bool tempValue;
                            if (!string.IsNullOrEmpty(output))
                                tempValue = Parse(output);
                            else
                                tempValue = false;

                            if (tempValue) //если перед нами 1 то идем сюда
                            {
                                ProgramData valueNext = DataGridTable[i + 1] as ProgramData;
                                var valueToNext = ValueBool(valueNext);
                                ProgramData valueNext2 = DataGridTable[i + 2] as ProgramData;
                                var memory = MemoryGridTable.Find(u => u.Address == valueNext2.Bit);
                                memory.CurrentValue = valueToNext.ToUpper();
                                DB[valueNext2.Bit] = memory.CurrentValue;
                            }
                            //если нет, то перескакиваем. считаем сколько перескачить
                            int count = 0;
                            for (int j = i; j <= item.Value; j++)
                            {
                                count++;
                                ProgramData valueNext = DataGridTable[j + 1] as ProgramData;
                                if (valueNext.Operator == value.AEM + ":")
                                {
                                    i = i + count;
                                    break;
                                }
                            }
                        }
                        else if (value.Operator.Contains("S"))
                        {
                            bool tempValue;
                            if (!string.IsNullOrEmpty(output))
                                tempValue = Parse(output);
                            else
                                tempValue = false;
                            if (tempValue)
                                DataWrite(value, "true");
                            else
                                ValueBool(value);

                            //Смотрим сл. строку, если там R или S то не обнуляем output
                            ProgramData valueNext = DataGridTable[i + 1] as ProgramData;
                            if (!valueNext.Operator.Contains("R"))
                                if (!valueNext.Operator.Contains("S"))
                                    output = "";
                        }
                        else if (value.Operator.Contains("R"))
                        {
                            bool tempValue;
                            if (!string.IsNullOrEmpty(output))
                                tempValue = Parse(output);
                            else
                                tempValue = false;
                            if (tempValue)
                                DataWrite(value, "false");
                            else
                                ValueBool(value);

                            //Смотрим сл. строку, если там R или S то не обнуляем output
                            ProgramData valueNext = DataGridTable[i + 1] as ProgramData;
                            if (!valueNext.Operator.Contains("R"))
                                if (!valueNext.Operator.Contains("S"))
                                    output = "";
                        }
                        else if (value.Operator.Contains("FP"))
                        {
                            var tempValue = Parse(output);
                            if (Convert.ToInt32(tempValue) != FrontP[value.Key.ToString()])
                            {
                                if (FrontP[value.Key.ToString()] == 0)
                                {
                                    FrontP[value.Key.ToString()] = 1;
                                    DataWrite(value, "true");
                                }
                                else
                                {
                                    DataWrite(value, "false");
                                    if (FrontP[value.Key.ToString()] == 1)
                                        FrontP[value.Key.ToString()] = 0;
                                    output = "";
                                    int count = 0;
                                    for (int j = i; j <= item.Value; j++)
                                    {
                                        count++;
                                        ProgramData valueNext = DataGridTable[j + 1] as ProgramData;
                                        if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                        {
                                            i = i + count - 1; //чтоб в нее зашло
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {      //Перескакиваем фронт
                                output = "";
                                int count = 0;
                                for (int j = i; j <= item.Value; j++)
                                {
                                    count++;
                                    ProgramData valueNext = DataGridTable[j + 1] as ProgramData;
                                    if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                    {
                                        i = i + count - 1; //чтоб в нее зашло
                                        break;
                                    }
                                }
                            }
                        }
                        else if (value.Operator.Contains("FN"))
                        {
                            var tempValue = Parse(output);
                            if (Convert.ToInt32(tempValue) != FrontN[value.Key.ToString()])
                            {
                                if (Convert.ToInt32(tempValue) == 1)
                                {
                                    FrontN[value.Key.ToString()] = 1;
                                    output = "";
                                    int count = 0;
                                    for (int j = i; j <= item.Value; j++)
                                    {
                                        count++;
                                        ProgramData valueNext = DataGridTable[j + 1] as ProgramData;
                                        if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                        {
                                            i = i + count - 1; //чтоб в нее зашло
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    if (FrontN[value.Key.ToString()] == 1)
                                    {
                                        DataWrite(value, "true");
                                        FrontN[value.Key.ToString()] = 0;
                                        output = "";
                                    }
                                }
                            }
                            else
                            //Перескакиваем в конец
                            {
                                output = "";
                                int count = 0;
                                for (int j = i; j <= item.Value; j++)
                                {
                                    count++;
                                    ProgramData valueNext = DataGridTable[j + 1] as ProgramData;
                                    if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                    {
                                        i = i + count - 1; //чтоб в нее зашло
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!thisOperator.Contains("("))
                                if (!value.Operator.Contains(")"))
                                    if (!value.Operator.Contains("L"))
                                        if (!value.Operator.Contains("="))
                                            if (!value.Operator.Contains("<>"))
                                                if (!value.Operator.Contains("<"))
                                                    if (!value.Operator.Contains(">"))
                                                        output += ValueBool(value);
                        }
                    }
                }
            }
        }
        #endregion
        #region Чтение и запись
        private string ReverseString(string s)
        {
            char[] value = s.ToCharArray();
            Array.Reverse(value);
            return new string(value);
        }
        private bool DataRead(string byteAndBit, string path)
        {
            var address = byteAndBit.Split('.');
            var count = 0;//-1
            int value;
            switch (path)
            {
                case "input":
                    value = InputData[Convert.ToInt32(address[0])];
                    break;
                case "marker":
                    value = MarkerData[Convert.ToInt32(address[0])];
                    break;
                default: //output
                    value = OutputData[Convert.ToInt32(address[0])];
                    break;
            }
            var bits = Convert.ToString(value, 2);
            while (bits.Length < 8)
                bits = bits.Insert(0, "0");
            bits = ReverseString(bits);
            foreach (char ch in bits)
            {
                if (count.ToString() == address[1])
                {
                    var tempString = ch.ToString();
                    var tempInt = Convert.ToInt32(tempString);
                    var tempBool = Convert.ToBoolean(tempInt);
                    return tempBool;
                }
                count++;
            }
            return false;
        }
        private bool DataWrite(ProgramData value, string output)
        {
            bool valueBool;
            try
            {
                valueBool = Parse(output);
            }
            catch
            {
                //МБ это уже и не нужно, не помню
                output = output.TrimEnd();
                output = output.Substring(0, output.LastIndexOf(' '));
                valueBool = Parse(output);
            }
            string path = "";

            if (value.AEM.Contains("A"))
            {
                path = "output";
                value.Output = Convert.ToInt32(valueBool).ToString();
            }
            if (value.AEM.Contains("M"))
            {
                path = "marker";
                value.Marker = Convert.ToInt32(valueBool).ToString();
            }
            if (value.AEM.Contains("E"))
            {
                path = "input";
                value.Input = Convert.ToInt32(valueBool).ToString();
            }
            if (value.AEM.Contains("DB"))
            {
                //Проверяем есть ли такое значение адреса в БД, если нет то это младший байт числа в другом адресе
                if (DB.ContainsKey(value.Bit))
                    DB[value.Bit] = valueBool.ToString();
                else
                {
                    var split = value.Bit.Split('.');
                    var olderByte = Convert.ToInt32(split[0]) - 1;
                    var valueOlderByte = Convert.ToString(Convert.ToInt32(DB[olderByte.ToString()]), 2);
                    while (valueOlderByte.Length < 8)
                        valueOlderByte = valueOlderByte.Insert(0, "0");
                    valueOlderByte = ReverseString(valueOlderByte);
                    valueOlderByte = valueOlderByte.Remove(Convert.ToInt16(split[1]), 1);
                    valueOlderByte = valueOlderByte.Insert(Convert.ToInt16(split[1]), Convert.ToInt16(valueBool).ToString());
                    valueOlderByte = ReverseString(valueOlderByte);
                    valueOlderByte = Convert.ToByte(valueOlderByte, 2).ToString();
                    DB[olderByte.ToString()] = valueOlderByte;
                    var memory2 = MemoryGridTable.Find(u => u.Address == olderByte.ToString());
                    memory2.CurrentValue = valueOlderByte.ToLower();
                    return true;
                }
                value.Output = Convert.ToInt32(valueBool).ToString();
                var memory = MemoryGridTable.Find(u => u.Address == value.Bit);
                if (memory == null)
                {
                    var tempAddress = value.Bit.Split('.');
                    memory = MemoryGridTable.Find(u => u.Address == tempAddress[0]);
                    var tempBits = Convert.ToString(Convert.ToInt32(memory.CurrentValue), 2);
                    while (tempBits.Length < 8)
                        tempBits = tempBits.Insert(0, "0");
                    tempBits = ReverseString(tempBits);
                    tempBits = tempBits.Remove(Convert.ToInt16(tempAddress[1]), 1);
                    tempBits = tempBits.Insert(Convert.ToInt16(tempAddress[1]), value.Output);
                    tempBits = ReverseString(tempBits);
                    memory.CurrentValue = Convert.ToByte(tempBits, 2).ToString();
                    DB[tempAddress[0]] = memory.CurrentValue;
                    return true;
                }
                memory.CurrentValue = valueBool.ToString().ToLower();
                DB[value.Bit] = memory.CurrentValue;
                return true;
            }
            var address = value.Bit.Split('.');
            int valueTemp;
            switch (path)
            {
                case "input":
                    valueTemp = InputData[Convert.ToInt32(address[0])];
                    break;
                case "marker":
                    valueTemp = MarkerData[Convert.ToInt32(address[0])];
                    break;
                default: //output
                    valueTemp = OutputData[Convert.ToInt32(address[0])];
                    break;
            }
            var bits = Convert.ToString(valueTemp, 2);
            while (bits.Length < 8)
                bits = bits.Insert(0, "0");
            bits = ReverseString(bits);
            bits = bits.Remove(Convert.ToInt16(address[1]), 1);
            bits = bits.Insert(Convert.ToInt16(address[1]), Convert.ToInt16(valueBool).ToString());
            bits = ReverseString(bits);

            var byteToSave = Convert.ToByte(bits, 2);
            switch (path)
            {
                case "input":
                    InputData[Convert.ToInt32(address[0])] = byteToSave;
                    break;
                case "marker":
                    MarkerData[Convert.ToInt32(address[0])] = byteToSave;
                    break;
                default: //output
                    OutputData[Convert.ToInt32(address[0])] = byteToSave;
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart)delegate ()
                        {
                            ServiceOutput();
                        }
                    );
                    break;
            }
            return true;
        }
        private string ValueBool(ProgramData value)
        {
            var valueBool = false;

            if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("E"))
            {
                valueBool = DataRead(value.Bit, "input");
                value.Input = Convert.ToInt32(valueBool).ToString();
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("M") && !value.AEM.Contains("MS") && !value.AEM.Contains("ms"))
            {
                valueBool = DataRead(value.Bit, "marker");
                value.Marker = Convert.ToInt32(valueBool).ToString();
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("A"))
            {
                valueBool = DataRead(value.Bit, "output");
                value.Output = Convert.ToInt32(valueBool).ToString(); //Стоял input
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("DB"))
            {
                string tempValue;
                if (DB.ContainsKey(value.Bit))
                    tempValue = DB[value.Bit].ToLower();
                else
                {
                    var split = value.Bit.Split('.');
                    var olderByte = Convert.ToInt32(split[0]) - 1;
                    var valueOlderByte = Convert.ToString(Convert.ToInt32(DB[olderByte.ToString()]), 2);
                    while (valueOlderByte.Length < 8)
                        valueOlderByte = valueOlderByte.Insert(0, "0");
                    valueOlderByte = ReverseString(valueOlderByte);
                    valueOlderByte = valueOlderByte.Substring(Convert.ToInt16(split[1]), 1);
                    if (valueOlderByte == "0")
                        tempValue = "false";
                    else
                        tempValue = "true";
                }

                if (tempValue.Contains("true") || tempValue.Contains("false"))
                    valueBool = Convert.ToBoolean(tempValue);
                else
                    return tempValue;
                value.Input = Convert.ToInt32(valueBool).ToString();
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("T") && value.Bit.Length > 0)
            {
                var containsTimer = TimerGridTable.Where(u => u.Address == value.Bit).SingleOrDefault();
                if (containsTimer != null)
                    valueBool = Convert.ToBoolean(Convert.ToInt32(containsTimer.Value));
                else
                    valueBool = false;
            }
            else if (!string.IsNullOrEmpty(value.AEM))
            {
                //Значит тут наверно число
                var valueInt = value.AEM;
                int result;
                int.TryParse(valueInt, out result);
                return result.ToString();
            }
            //Проверяем на негатив
            if (value.Operator.Substring(value.Operator.Length - 1, 1) == "N")
                valueBool = Parse("!" + valueBool);

            return valueBool.ToString();
        }
        #endregion
        #region Изменение времени стекания
        private void SaveTextBoxes(DependencyObject obj)
        {
            TextBox tb = obj as TextBox;
            if (tb != null)
            {
                var newTime = 0;
                if (!string.IsNullOrEmpty(tb.Text.Trim()))
                    newTime = Convert.ToInt32(tb.Text.Trim());
                switch (tb.Name)
                {
                    case "Stek16":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(16));
                        break;
                    case "Stek_17_18":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(17));
                        break;
                    case "Stek_19":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(18));
                        break;
                    case "Stek_20":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(19));
                        break;
                    case "Stek_21":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(20));
                        break;
                    case "Stek_22":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(21));
                        break;
                    case "Stek_23":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(22));
                        break;
                    case "Stek_24_25":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(23));
                        break;
                    case "Stek_5_7":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(25));
                        break;
                    case "Stek_8_10":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(26));
                        break;
                    case "Stek_9":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(27));
                        break;
                    case "Stek_11":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(28));
                        break;
                    case "Stek_12":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(29));
                        break;
                    case "Stek_13":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(30));
                        break;
                    case "Stek_14":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(31));
                        break;
                    case "Stek_15":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(32));
                        break;
                }
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj as DependencyObject); i++)
                SaveTextBoxes(VisualTreeHelper.GetChild(obj, i));
        }
        private void SaveToFile(string Stek, int newTime, KeyValuePair<string, string> DBAdress)
        {
            if (DB[DBAdress.Key] == "S5T#" + newTime + "S") //Если неизменилось значение, то выходим
                return;

            DB[DBAdress.Key] = "S5T#" + newTime + "S";
            this.Stek[Stek] = newTime * 1000;
            int start = 0;
            var allLines = new List<string>();
            using (StreamReader fs = new StreamReader(Path, Encoding.Default))
            {
                while (true)
                {
                    start++;
                    string tempLine = fs.ReadLine();
                    if (tempLine == null)
                        break;
                    if (tempLine.Contains(Stek))
                    {
                        var tempLines = tempLine.Split(':');
                        if (tempLines.Count() == 3)
                        {
                            var komment = tempLines[2].Split('/');
                            string komm = "";
                            if (komment.Count() > 1)
                                komm = "//" + komment[2];
                            allLines.Add(tempLines[0] + ":" + tempLines[1] + ":= S5T#" + Convert.ToInt32(newTime) + "S;	" + komm);
                        }
                        else
                            allLines.Add(tempLine);
                    }
                    else
                        allLines.Add(tempLine);
                }
            }
            using (var fileStream = new FileStream(Path, FileMode.Open))
            using (var streamWriter = new StreamWriter(fileStream, Encoding.Default))
            {
                foreach (var item in allLines)
                    streamWriter.WriteLine(item);
            }
        }
        #endregion
        #region Функции самой программы
        private void button_Menu_Click(object sender, RoutedEventArgs e)
        {
            switch (((Button)sender).TabIndex)
            {
                case 1:
                    if (!backgroundWorker.IsBusy)
                    {
                        ResetAll(); //Возможно избыточно
                        backgroundWorker.RunWorkerAsync();
                    }
                    else
                    {
                        if (DB["0.3"] == "true")
                            MessageBox.Show("Для того чтобы запустить программу с нуля, нужно выключить автоматический режим");
                        else
                            ResetAll();
                    }
                    tabControl.SelectedIndex = 0;
                    break;
                case 2:
                    tabControl.SelectedIndex = 1;
                    break;
                case 3:
                    InputData[0] = 127;
                    break;
                case 4:
                    tabControl.SelectedIndex = 3;
                    break;
                case 5:
                    ResetAll();
                    this.Close();
                    break;
                default:
                    break;
            }
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1:
                    if (!backgroundWorker.IsBusy)
                    {
                        ResetAll(); //Возможно избыточно
                        backgroundWorker.RunWorkerAsync();
                    }
                    else
                    {
                        if (DB["0.3"] == "true")
                            MessageBox.Show("Для того чтобы запустить программу с нуля, нужно выключить автоматический режим");
                        else
                            ResetAll();
                    }
                    tabControl.SelectedIndex = 0;
                    button_Start.Focus();
                    break;
                case Key.F2:
                    tabControl.SelectedIndex = 1;
                    button_Stekanie.Focus();
                    break;
                case Key.F3:
                    InputData[0] = 127;
                    button_Info.Focus();
                    break;
                case Key.F4:
                    tabControl.SelectedIndex = 3;
                    button_Service.Focus();
                    break;
                case Key.F10:
                    ResetAll();
                    this.Close();
                    break;
                default:
                    break;
            }
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ResetAll();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveTextBoxes(tabControl);
        }
        private Boolean IsTextAllowed(String text)
        {
            return Array.TrueForAll<Char>(text.ToCharArray(),
                delegate (Char c) { return Char.IsDigit(c) || Char.IsControl(c); });
        }
        private void PreviewTextInputHandler(Object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }
        private void PastingHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }
        private void FillTextBoxes()
        {
            Stek16.Text = (Stek[Stek16.Name] / 1000).ToString();
            Stek_17_18.Text = (Stek[Stek_17_18.Name] / 1000).ToString();
            Stek_19.Text = (Stek[Stek_19.Name] / 1000).ToString();
            Stek_20.Text = (Stek[Stek_20.Name] / 1000).ToString();
            Stek_21.Text = (Stek[Stek_21.Name] / 1000).ToString();
            Stek_22.Text = (Stek[Stek_22.Name] / 1000).ToString();
            Stek_23.Text = (Stek[Stek_23.Name] / 1000).ToString();
            Stek_24_25.Text = (Stek[Stek_24_25.Name] / 1000).ToString();
            Stek_5_7.Text = (Stek[Stek_5_7.Name] / 1000).ToString();
            Stek_8_10.Text = (Stek[Stek_8_10.Name] / 1000).ToString();
            Stek_9.Text = (Stek[Stek_9.Name] / 1000).ToString();
            Stek_11.Text = (Stek[Stek_11.Name] / 1000).ToString();
            Stek_12.Text = (Stek[Stek_12.Name] / 1000).ToString();
            Stek_13.Text = (Stek[Stek_13.Name] / 1000).ToString();
            Stek_14.Text = (Stek[Stek_14.Name] / 1000).ToString();
            Stek_15.Text = (Stek[Stek_15.Name] / 1000).ToString();
        }
        #endregion
        #region Обновление данных в сервисном режиме и на плате, считываение с платы
        private void ServiceOutput()
        {
            //Здесь еще надо обновить данные на плате, т.е. отправить Output
            for (int i = 0; i < 3; i++)
            {
                var tempBits = Convert.ToString(OutputData[i], 2);
                while (tempBits.Length < 8)
                    tempBits = tempBits.Insert(0, "0");
                tempBits = ReverseString(tempBits);
                for (int j = 0; j <= tempBits.Length; j++)
                {
                    CheckBox chekbox = tabControl.FindName("Output" + i + "Bit" + j) as CheckBox;
                    if (chekbox != null && tempBits[j] == '1')
                        chekbox.IsChecked = true;
                }
            }
        }
        private void timer_Tick_Input(object sender, EventArgs e)
        {
            //Считываем с платы и обновляем InputData
            for (int i = 0; i < 4; i++)
            {
                var tempBits = Convert.ToString(InputData[i], 2);
                while (tempBits.Length < 8)
                    tempBits = tempBits.Insert(0, "0");
                tempBits = ReverseString(tempBits);
                for (int j = 0; j <= tempBits.Length; j++)
                {
                    CheckBox chekbox = tabControl.FindName("Input" + i + "Bit" + j) as CheckBox;
                    if (chekbox != null)
                    {
                        if (tempBits[j] == '1')
                            chekbox.IsChecked = true;
                        else
                            chekbox.IsChecked = false;
                    }
                }
            }
            //Обновляем таблицу действий операторов
            zadOp1.Content = DB["46"];
            zadOp2.Content = DB["44"];
            istOp1.Content = DB["52"];
            istOp2.Content = DB["42"];
            rasOp1.Content = DB["50"];
            rasOp2.Content = DB["40"];

            if (DB["0.2"].ToLower() == "true")
                deyOp1.Content = "Исходное";
            else if (DB["0.5"].ToLower() == "true")
                deyOp1.Content = "Ошибка позиции";
            else if (DB["1.0"].ToLower() == "true")
                deyOp1.Content = "Ожидание";
            else if (DB["1.1"].ToLower() == "true")
                deyOp1.Content = "Подъем";
            else if (DB["1.2"].ToLower() == "true")
                deyOp1.Content = "Опускание";
            else if (DB["1.5"].ToLower() == "true")
                deyOp1.Content = "Ход влево";
            else if (DB["1.6"].ToLower() == "true")
                deyOp1.Content = "Ход вправо";
            else if (DB["1.7"].ToLower() == "true")
                deyOp1.Content = "Ошибка датчиков";
            else if (DB["54.4"].ToLower() == "true")
                deyOp1.Content = "Ожидание загрузки";

            if (DB["0.1"].ToLower() == "true")
                deyOp2.Content = "Исходное";
            else if (DB["0.6"].ToLower() == "true")
                deyOp2.Content = "Ошибка позиции";
            else if (DB["0.7"].ToLower() == "true")
                deyOp2.Content = "Ожидание";
            else if (DB["1.3"].ToLower() == "true")
                deyOp2.Content = "Подъем";
            else if (DB["1.4"].ToLower() == "true")
                deyOp2.Content = "Опускание";
            else if (DB["54.0"].ToLower() == "true")
                deyOp2.Content = "Ход влево";
            else if (DB["54.1"].ToLower() == "true")
                deyOp2.Content = "Ход вправо";
            else if (DB["54.2"].ToLower() == "true")
                deyOp2.Content = "Ошибка датчиков";
            else if (DB["54.4"].ToLower() == "true")
                deyOp2.Content = "Ожидание загрузки";
        }
        private void ResetAll()
        {
            //InputData = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0 };
            MarkerData = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            OutputData = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0 };
            //CБрасываем еще выхода на плате в 0.
        }
        #endregion
    }
}