using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace pas1
{
    public partial class Form1 : Form
    {
        private SqlConnection connection;
        public Form1()
        {
            InitializeComponent();
        }
        public void ClearDatagrid(DataGridView dataGridView)
        {
            dataGridView.DataSource = null;
            dataGridView.Columns.Clear();
            dataGridView.Rows.Clear();
        }
        SqlCommand command;
        private bool isQueryMode = false;

        private void Form1_Load(object sender, EventArgs e)
        {
            // Инициализация объекта подключения
            connection = new SqlConnection(
                // Получение значения строки подключения из файла App.config
                ConfigurationManager.ConnectionStrings["dbKey"].ConnectionString
                );
            // Открытие подключения
            connection.Open();
            // Проверка успешности подключения к БД
            if (connection.State == ConnectionState.Open)
                // Сообщение об успешном подключении к БД
                MessageBox.Show("Подключение к базе выполнено успешно");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Очищаем ComboBox перед добавлением новых элементов, чтобы не было дублирования
            comboBox1.Items.Clear();

            List<List<string>> res = new List<List<string>>();
            command = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", // SQL запрос
            connection);
            SqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows) // Если результат не пустой
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    reader.GetName(i); // Получение имени столбца i
                }
                while (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        comboBox1.Items.Add(reader.GetValue(i).ToString()); // Получение значения столбца i текущей строки
                    }
                }
            }
            reader.Close();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            command = new SqlCommand("select * from " + comboBox1.SelectedItem.ToString() + ";", // SQL запрос
            connection);
            SqlDataReader reader = command.ExecuteReader();

            ClearDatagrid(dataGridView1);

            if (reader.HasRows) // Если результат не пустой
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var column2 = new DataGridViewColumn();
                    column2.CellTemplate = new DataGridViewTextBoxCell();
                    column2.HeaderText = reader.GetName(i);
                    dataGridView1.Columns.Add(column2);
                }
                int row = 0;
                while (reader.Read())
                {
                    dataGridView1.Rows.Add();
                    dataGridView1.Rows[row].HeaderCell.Value = (row + 1).ToString();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        dataGridView1[i, row].Value = reader.GetValue(i).ToString(); // Получение значения столбца i текущей строки
                    }
                    row++;
                }
            }
            reader.Close();

            isQueryMode = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (isQueryMode)
            {
                MessageBox.Show("Добавление данных запрещено при просмотре заданий!");
                return;
            }
            if (comboBox1.SelectedItem == null)
            {
                MessageBox.Show("Выберите таблицу для добавления строки.");
                return;
            }
            // Получаем выбранную таблицу
            string tableName = comboBox1.SelectedItem.ToString();

            // Список автоинкрементных колонок
            List<string> identityColumns = new List<string>();

            // Запрос для получения информации о колонках с автоинкрементом
            string identityQuery = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMNPROPERTY(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1";

            // Выполняем запрос для получения автоинкрементных колонок
            using (SqlCommand identityCmd = new SqlCommand(identityQuery, connection))
            {
                SqlDataReader reader = identityCmd.ExecuteReader();
                while (reader.Read())
                {
                    identityColumns.Add(reader.GetString(0)); // Добавляем имя колонки
                }
                reader.Close();
            }

            // Получаем последнюю строку из DataGridView (где введены новые данные)
            int rowIndex = dataGridView1.Rows.Count - 2; // Индекс последней строки
            DataGridViewRow row = dataGridView1.Rows[rowIndex];

            // Списки для хранения имен колонок и соответствующих значений
            List<string> columnNames = new List<string>();//Хранение имени колонки
            List<string> parameterNames = new List<string>();//Хранение параметра для SQL запроса
            List<object> values = new List<object>();//Хранение значения ячейки

            // Проходим по всем столбцам строки
            foreach (DataGridViewCell cell in row.Cells)
            {
                string columnName = dataGridView1.Columns[cell.ColumnIndex].HeaderText; // Имя столбца
                object cellValue = cell.Value; // Значение ячейки

                // Пропускаем автоинкрементные колонки
                if (identityColumns.Contains(columnName))
                {
                    continue; // Пропускаем колонку с автоинкрементом
                }

                // Проверка, что значение не пустое (не вставляем пустые значения)
                if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                {
                    columnNames.Add(columnName); // Добавляем имя колонки
                    parameterNames.Add("@" + columnName); // Создаем параметр для SQL запроса
                    values.Add(cellValue); // Сохраняем значение
                }
            }

            // Проверяем, есть ли данные для вставки
            if (columnNames.Count == 0)
            {
                MessageBox.Show("Все поля пустые или содержат только автоинкрементные колонки. Введите данные для добавления.");
                return;
            }

            // Формируем SQL запрос для вставки данных
            string query = $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", parameterNames)})";

            try
            {
                // Создаем команду для выполнения SQL запроса
                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    // Добавляем параметры в команду
                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        cmd.Parameters.AddWithValue(parameterNames[i], values[i]);
                    }

                    // Выполняем запрос
                    int rowsAffected = cmd.ExecuteNonQuery();

                    // Проверка успешности добавления данных
                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Данные успешно добавлены!");
                    }
                    else
                    {
                        MessageBox.Show("Не удалось добавить данные.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Обрабатываем возможные ошибки
                MessageBox.Show("Ошибка при добавлении данных: " + ex.Message);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (isQueryMode)
            {
                MessageBox.Show("Удаление данных запрещено при просмотре заданий!");
                return;
            }
            //Проверка выбранных строк
            if (dataGridView1.SelectedRows.Count > 0)
            {
                int selectedIndex = dataGridView1.SelectedRows[0].Index;
                string recordId = dataGridView1[0, selectedIndex].Value.ToString();
                string recordIdSec = dataGridView1[1, selectedIndex].Value.ToString();
                string tableName = comboBox1.Text;

                try
                {
                    string query = $"DELETE FROM {tableName} WHERE " + dataGridView1.Columns[0].HeaderText.ToString() + " = @recordId";

                    //Обработка исключения для таблицы order_items
                    if (tableName == "order_items")
                    {
                        query = $"DELETE FROM {tableName} WHERE " + dataGridView1.Columns[0].HeaderText.ToString() + " = @recordId" + " AND " + dataGridView1.Columns[1].HeaderText.ToString() + " = @recordIdSec";
                    }

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@recordId", recordId);

                        // Обработка исключения для таблицы order_items
                        if (tableName == "order_items")
                        {
                            cmd.Parameters.AddWithValue("@recordIdSec", recordIdSec);
                        }
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            dataGridView1.Rows.RemoveAt(selectedIndex);
                            MessageBox.Show("Запись успешно удалена.");
                        }
                        else
                        {
                            MessageBox.Show("Не удалось удалить запись.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при удалении записи: " + ex.Message);
                }

            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите строку для удаления.");
            }
        }


        private void button4_Click(object sender, EventArgs e)
        {
            ClearDatagrid(dataGridView1); // Очистка DataGridView перед новым выводом

            string query = @"
                SELECT 
                    e.full_name AS 'ФИО',
                    q.position AS 'Должность',
                    sr.amount_to_issue AS 'Начислено',
                    ROUND(sr.amount_to_issue * sr.withholdings / 100, 2) AS 'Удержано',
                    ROUND(sr.amount_to_issue - (sr.amount_to_issue * sr.withholdings / 100), 2) AS 'К выдаче'
                FROM 
                    salary_records sr
                JOIN 
                    employees e ON sr.employee_id = e.employee_id
                JOIN 
                    qualifications q ON e.employee_id = q.employee_id
                WHERE 
                    sr.month_year = (SELECT MAX(month_year) FROM salary_records)
                ORDER BY 
                    q.position, e.full_name;";

            try
            {
                SqlCommand cmd = new SqlCommand(query, connection);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable table = new DataTable();
                adapter.Fill(table);

                dataGridView1.DataSource = table;
                isQueryMode = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения запроса", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            ClearDatagrid(dataGridView1); // Очистка DataGridView перед новым выводом

            string query = @"
                SELECT 
                    q.position AS 'Должность',
                    SUM(sr.working_days - sr.actual_days_worked) AS 'Пропущено дней',
                    ROUND(AVG(sr.bonus), 2) AS 'Средняя премия'
                FROM 
                    salary_records sr
                JOIN 
                    qualifications q ON sr.employee_id = q.employee_id
                GROUP BY 
                    q.position
                ORDER BY 
                    [Пропущено дней] DESC;";

            try
            {
                SqlCommand cmd = new SqlCommand(query, connection);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable table = new DataTable();
                adapter.Fill(table);

                dataGridView1.DataSource = table;
                isQueryMode = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения запроса", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            ClearDatagrid(dataGridView1); // Очистка DataGridView перед новым выводом

            string query = @"
                SELECT 
                    q.education AS 'Образование',
                    COUNT(*) AS 'Количество сотрудников'
                FROM 
                    qualifications q
                GROUP BY 
                    q.education;";

            try
            {
                SqlCommand cmd = new SqlCommand(query, connection);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable table = new DataTable();
                adapter.Fill(table);

                dataGridView1.DataSource = table;
                isQueryMode = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения запроса", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            string sqlCommands = @"
                IF OBJECT_ID('reprimands', 'U') IS NOT NULL DROP TABLE reprimands;
                IF OBJECT_ID('qualifications_promotions', 'U') IS NOT NULL DROP TABLE qualifications_promotions;
                IF OBJECT_ID('salary_records', 'U') IS NOT NULL DROP TABLE salary_records;
                IF OBJECT_ID('qualifications', 'U') IS NOT NULL DROP TABLE qualifications;
                IF OBJECT_ID('employment_details', 'U') IS NOT NULL DROP TABLE employment_details;
                IF OBJECT_ID('employees', 'U') IS NOT NULL DROP TABLE employees;

                CREATE TABLE employees (
                    employee_id INT IDENTITY PRIMARY KEY,
                    full_name NVARCHAR(255) NOT NULL,
                    home_address NVARCHAR(MAX) NOT NULL,
                    home_phone NVARCHAR(15),
                    work_phone NVARCHAR(15)
                );

                CREATE TABLE employment_details (
                    employee_id INT PRIMARY KEY REFERENCES employees(employee_id) ON DELETE CASCADE,
                    hire_date DATE NOT NULL,
                    specialty_experience INT DEFAULT 0 CHECK (specialty_experience >= 0),
                    total_experience INT DEFAULT 0 CHECK (total_experience >= 0)
                );

                CREATE TABLE qualifications (
                    employee_id INT PRIMARY KEY REFERENCES employees(employee_id) ON DELETE CASCADE,
                    education NVARCHAR(255) NOT NULL,
                    qualification NVARCHAR(255) NOT NULL,
                    position NVARCHAR(255) NOT NULL,
                    salary_rate DECIMAL(10, 2) NOT NULL CHECK (salary_rate > 0)
                );

                CREATE TABLE salary_records (
                    record_id INT IDENTITY PRIMARY KEY,
                    employee_id INT REFERENCES employees(employee_id) ON DELETE CASCADE,
                    month_year DATE NOT NULL,
                    working_days INT NOT NULL CHECK (working_days > 0),
                    actual_days_worked INT NOT NULL CHECK (actual_days_worked >= 0),
                    bonus DECIMAL(10, 2) DEFAULT 0 CHECK (bonus >= 0),
                    vacation_pay DECIMAL(10, 2) DEFAULT 0 CHECK (vacation_pay >= 0),
                    withholdings DECIMAL(5, 2) DEFAULT 0 CHECK (withholdings BETWEEN 0 AND 100),
                    advance DECIMAL(10, 2) DEFAULT 0 CHECK (advance >= 0),
                    amount_to_issue DECIMAL(10, 2) NOT NULL,
                    CONSTRAINT check_days CHECK (actual_days_worked <= working_days)
                );

                CREATE TABLE qualifications_promotions (
                    promotion_id INT IDENTITY PRIMARY KEY,
                    employee_id INT REFERENCES employees(employee_id) ON DELETE CASCADE,
                    bonus_amount DECIMAL(10, 2) DEFAULT 1 CHECK (bonus_amount > 0),
                    promotion_date DATE NOT NULL
                );

                CREATE TABLE reprimands (
                    reprimand_id INT IDENTITY PRIMARY KEY,
                    employee_id INT REFERENCES employees(employee_id) ON DELETE CASCADE,
                    penalty_amount DECIMAL(10, 2) DEFAULT 2 CHECK (penalty_amount > 0),
                    reprimand_date DATE NOT NULL
                );

                INSERT INTO employees (full_name, home_address, home_phone, work_phone)
                VALUES 
                (N'Иванов Иван Иванович', N'г. Москва, ул. Ленина, д. 10', N'+79001234567', N'+74951234567'),
                (N'Петров Петр Петрович', N'г. Санкт-Петербург, ул. Пушкина, д. 5', N'+79009876543', N'+78129876543'),
                (N'Сидоров Сидор Сидорович', N'г. Казань, ул. Баумана, д. 1', N'+79001112233', N'+78431112233'),
                (N'Кузнецов Алексей Сергеевич', N'г. Новосибирск, ул. Красный проспект, д. 20', N'+79131234567', N'+73831234567'),
                (N'Морозова Анна Владимировна', N'г. Екатеринбург, ул. Малышева, д. 88', N'+79229876543', N'+73439876543'),
                (N'Федоров Дмитрий Петрович', N'г. Самара, ул. Ленинградская, д. 45', N'+79876543210', N'+78466543210');

                INSERT INTO employment_details (employee_id, hire_date, specialty_experience, total_experience)
                VALUES 
                (1, '2015-06-01', 8, 10),
                (2, '2018-09-15', 5, 7),
                (3, '2020-03-01', 3, 5),
                (4, '2017-04-10', 6, 8),
                (5, '2019-07-22', 4, 6),
                (6, '2021-01-15', 2, 4);

                INSERT INTO qualifications (employee_id, education, qualification, position, salary_rate)
                VALUES 
                (1, N'Высшее', N'Инженер', N'Старший инженер', 100000),
                (2, N'Среднее специальное', N'Техник', N'Техник', 50000),
                (3, N'Среднее', N'Оператор', N'Оператор', 30000),
                (4, N'Высшее', N'Инженер', N'Инженер', 80000),
                (5, N'Среднее специальное', N'Лаборант', N'Лаборант', 45000),
                (6, N'Среднее', N'Оператор', N'Оператор', 35000);

                INSERT INTO salary_records (employee_id, month_year, working_days, actual_days_worked, bonus, vacation_pay, withholdings, advance, amount_to_issue)
                VALUES 
                (1, '2023-10-01', 22, 20, 5000, 0, 13, 20000, 80000),
                (2, '2023-10-01', 22, 22, 3000, 0, 13, 10000, 40000),
                (3, '2023-10-01', 22, 18, 2000, 0, 13, 5000, 20000),
                (1, '2023-09-01', 21, 20, 4000, 0, 13, 20000, 75000),
                (2, '2023-09-01', 21, 21, 2000, 0, 13, 10000, 35000),
                (3, '2023-09-01', 21, 19, 1000, 0, 13, 5000, 18000),
                (1, '2023-08-01', 23, 22, 6000, 0, 13, 20000, 85000),
                (2, '2023-08-01', 23, 23, 3000, 0, 13, 10000, 42000),
                (3, '2023-08-01', 23, 20, 1500, 0, 13, 5000, 22000),
                (4, '2023-10-01', 22, 21, 4000, 0, 13, 15000, 65000),
                (5, '2023-10-01', 22, 20, 2500, 0, 13, 8000, 37000),
                (6, '2023-10-01', 22, 19, 1800, 0, 13, 6000, 29000),
                (4, '2023-09-01', 21, 20, 3500, 0, 13, 15000, 60000),
                (5, '2023-09-01', 21, 19, 2000, 0, 13, 8000, 34000),
                (6, '2023-09-01', 21, 18, 1500, 0, 13, 6000, 27000),
                (4, '2023-08-01', 23, 22, 4500, 0, 13, 15000, 70000),
                (5, '2023-08-01', 23, 21, 2800, 0, 13, 8000, 39000),
                (6, '2023-08-01', 23, 20, 1700, 0, 13, 6000, 30000);

                INSERT INTO qualifications_promotions (employee_id, promotion_date, bonus_amount)
                VALUES 
                (1, '2023-06-01', 1.50),
                (4, '2023-07-15', 1.00);

                INSERT INTO reprimands (employee_id, reprimand_date, penalty_amount)
                VALUES 
                (2, '2023-08-10', 2.50),
                (2, '2023-07-10', 2.00),
                (5, '2023-09-05', 2.00);
                ";

            try
            {
                SqlCommand cmd = new SqlCommand(sqlCommands, connection);
                cmd.ExecuteNonQuery();
                MessageBox.Show("База данных приведена к исходному виду", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            ClearDatagrid(dataGridView1); // Очистка DataGridView перед новым выводом

            string query = @"
                WITH avg_salary AS (
                    SELECT 
                        ROUND(AVG(amount_to_issue), 2) AS [Средняя зарплата]
                    FROM 
                        salary_records
                    WHERE 
                        month_year IN (
                            SELECT TOP 3 month_year 
                            FROM salary_records 
                            GROUP BY month_year 
                            ORDER BY month_year DESC
                        )
                )
                SELECT 
                    e.full_name AS [ФИО]
                FROM 
                    salary_records sr
                JOIN 
                    employees e ON sr.employee_id = e.employee_id
                WHERE 
                    sr.month_year IN (
                        SELECT TOP 3 month_year 
                        FROM salary_records 
                        GROUP BY month_year 
                        ORDER BY month_year DESC
                    )
                GROUP BY 
                    e.full_name
                HAVING 
                    AVG(sr.amount_to_issue) > (SELECT [Средняя зарплата] FROM avg_salary)
                ORDER BY 
                    e.full_name;";

            try
            {
                SqlCommand cmd = new SqlCommand(query, connection);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable table = new DataTable();
                adapter.Fill(table);

                dataGridView1.DataSource = table;
                isQueryMode = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения запроса", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            ClearDatagrid(dataGridView1); // Очистка DataGridView перед новым выводом

            string query = @"
                WITH total_salary AS (
                    SELECT 
                        SUM(amount_to_issue) AS total_fund
                    FROM 
                        salary_records
                ),
                education_salary AS (
                    SELECT 
                        q.education,
                        SUM(sr.amount_to_issue) AS education_fund
                    FROM 
                        salary_records sr
                    JOIN 
                        qualifications q ON sr.employee_id = q.employee_id
                    GROUP BY 
                        q.education
                )
                SELECT 
                    es.education AS [Образование],
                    ROUND((es.education_fund / ts.total_fund) * 100, 2) AS [Доля в процентах]
                FROM 
                    education_salary es
                CROSS JOIN 
                    total_salary ts;";

            try
            {
                SqlCommand cmd = new SqlCommand(query, connection);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable table = new DataTable();
                adapter.Fill(table);

                dataGridView1.DataSource = table;
                isQueryMode = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения запроса", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            ClearDatagrid(dataGridView1); // Очистка DataGridView перед новым выводом

            string query = richTextBox1.Text;

            try
            {
                SqlCommand cmd = new SqlCommand(query, connection);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable table = new DataTable();
                adapter.Fill(table);

                dataGridView1.DataSource = table;
                isQueryMode = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения запроса", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
