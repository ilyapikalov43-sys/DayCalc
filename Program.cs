using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace ConstructionDaysCalculator
{
    class Program
    {
        static string dbPath = "construction.db";

        static void Main(string[] args)
        {
            Console.WriteLine("Запускаем калькулятор с БД...");

            InitializeDatabase();

            try
            {
                DateTime startDate = GetDate("Введите дату начала работ (дд.мм.гггг): ");
                DateTime endDate = GetDate("Введите дату окончания работ (дд.мм.гггг): ");

                if (endDate < startDate)
                {
                    Console.WriteLine("Дата конца раньше чем начала!");
                    return;
                }

                Console.WriteLine("Выберите график работы:");
                Console.WriteLine("1. Пятидневка (5/2)");
                Console.WriteLine("2. Шестидневка (6/1)");
                Console.Write("Ввод (1 или 2): ");
                string scheduleChoice = Console.ReadLine();
                int scheduleType = (scheduleChoice == "2") ? 6 : 5;

                int workingDays = CalculateWorkingDays(startDate, endDate, scheduleType);
                SaveProject(startDate, endDate, scheduleType, workingDays);

                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($"Результат: {workingDays} рабочих дней.");
                Console.WriteLine("Расчёт сохранён в базу (construction.db).");
                Console.WriteLine("Готово!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Что-то не так: {ex.Message}");
            }

            Console.WriteLine("Нажми любую клавишу, чтобы выйти...");
            Console.ReadKey();
        }

        static void InitializeDatabase()
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();

                var cmdHoliday = connection.CreateCommand();
                cmdHoliday.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Holidays (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Day INTEGER NOT NULL,
                        Month INTEGER NOT NULL,
                        Name TEXT
                    )";
                cmdHoliday.ExecuteNonQuery();

                var cmdProject = connection.CreateCommand();
                cmdProject.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Projects (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        StartDate TEXT,
                        EndDate TEXT,
                        ScheduleType INTEGER,
                        Result INTEGER,
                        CreatedAt TEXT
                    )";
                cmdProject.ExecuteNonQuery();

                var cmdCheck = connection.CreateCommand();
                cmdCheck.CommandText = "SELECT COUNT(*) FROM Holidays";
                int count = Convert.ToInt32(cmdCheck.ExecuteScalar());

                if (count == 0)
                {
                    Console.WriteLine("Заполняем базу праздниками...");

                    var holidays = new List<(int Day, int Month, string Name)>
                    {
                        (1, 1, "Новый год"), (2, 1, "Новый год"), (7, 1, "Рождество"),
                        (8, 3, "8 марта"), (1, 5, "День труда"), (9, 5, "День победы"),
                        (12, 6, "День России"), (4, 11, "День единства")
                    };

                    var cmdInsert = connection.CreateCommand();
                    cmdInsert.CommandText = "INSERT INTO Holidays (Day, Month, Name) VALUES (@d, @m, @n)";

                    // ВАЖНО: Добавляем все параметры заранее
                    cmdInsert.Parameters.Add("@d", SqliteType.Integer);
                    cmdInsert.Parameters.Add("@m", SqliteType.Integer);
                    cmdInsert.Parameters.Add("@n", SqliteType.Text);

                    foreach (var h in holidays)
                    {
                        cmdInsert.Parameters["@d"].Value = h.Day;
                        cmdInsert.Parameters["@m"].Value = h.Month;
                        cmdInsert.Parameters["@n"].Value = h.Name;
                        cmdInsert.ExecuteNonQuery();
                    }
                }
            }
        }

        static DateTime GetDate(string message)
        {
            while (true)
            {
                Console.Write(message);
                string input = Console.ReadLine();
                if (DateTime.TryParse(input, out DateTime date))
                {
                    return date;
                }
                Console.WriteLine("Введите правильную дату!");
            }
        }

        static int CalculateWorkingDays(DateTime start, DateTime end, int workDaysWeek)
        {
            int count = 0;
            DateTime current = start;

            List<(int, int)> dbHolidays = GetHolidaysFromDb();

            while (current <= end)
            {
                if (dbHolidays.Exists(h => h.Item1 == current.Day && h.Item2 == current.Month))
                {
                    current = current.AddDays(1);
                    continue;
                }

                if (IsWeekend(current, workDaysWeek))
                {
                    current = current.AddDays(1);
                    continue;
                }

                count++;
                current = current.AddDays(1);
            }

            return count;
        }

        static List<(int, int)> GetHolidaysFromDb()
        {
            var holidays = new List<(int, int)>();
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Day, Month FROM Holidays";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        holidays.Add((Convert.ToInt32(reader[0]), Convert.ToInt32(reader[1])));
                    }
                }
            }
            return holidays;
        }

        static void SaveProject(DateTime start, DateTime end, int schedule, int result)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Projects (StartDate, EndDate, ScheduleType, Result, CreatedAt) 
                    VALUES (@s, @e, @t, @r, @c)";
                cmd.Parameters.AddWithValue("@s", start.ToString());
                cmd.Parameters.AddWithValue("@e", end.ToString());
                cmd.Parameters.AddWithValue("@t", schedule);
                cmd.Parameters.AddWithValue("@r", result);
                cmd.Parameters.AddWithValue("@c", DateTime.Now.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        static bool IsWeekend(DateTime date, int workDaysWeek)
        {
            DayOfWeek day = date.DayOfWeek;
            if (workDaysWeek == 5)
                return (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday);
            else if (workDaysWeek == 6)
                return (day == DayOfWeek.Sunday);
            return false;
        }
    }
}