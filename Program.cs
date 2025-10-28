using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;






class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Fetching data from API...");

        string apiUrl = "https://rc-vault-fap-live-1.azurewebsites.net/api/gettimeentries?code=vO17RnE8vuzXzPJo5eaLLjXjmRW07law99QTD90zat9FfOQJKKUcgQ=="; 

        try
        {
            using HttpClient client = new HttpClient();
            var response = await client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var timeEntries = JsonSerializer.Deserialize<List<TimeEntry>>(json, options);

            if (timeEntries == null || timeEntries.Count == 0)
            {
                Console.WriteLine("No time entries found.");
                return;
            }

            var validEntries = timeEntries
                .Where(e => e.DeletedOn == null && !string.IsNullOrEmpty(e.EmployeeName))
                .ToList();

            var groupedData = validEntries
                .GroupBy(e => e.EmployeeName)
                .Select(g => new
                {
                    EmployeeName = g.Key!, 
                    TotalHours = g.Sum(e => (e.EndTimeUtc - e.StarTimeUtc).TotalHours)
                })
                .OrderByDescending(x => x.TotalHours)
                .ToList();

            string[] labels = groupedData.Select(g => g.EmployeeName).ToArray();
            double[] values = groupedData.Select(g => Math.Round(g.TotalHours, 2)).ToArray();

            string labelsJson = JsonSerializer.Serialize(labels);
            string valuesJson = JsonSerializer.Serialize(values);

            string[] colors = values.Select(v => v < 100 ? "#FFF59D" : "rgba(54, 162, 235, 0.7)").ToArray();
            string colorsJson = JsonSerializer.Serialize(colors);

            string htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Employee Time Report</title>
    <style>
        body {{ font-family: Arial; margin: 40px; }}
        h2 {{ color: #2c3e50; }}
        table {{ border-collapse: collapse; width: 80%; margin-top: 20px; }}
        th, td {{ border: 1px solid #ccc; padding: 10px; text-align: left; }}
        th {{ background-color: #f4f4f4; }}
        .chart {{ max-width: 800px; margin-top: 30px; }}
    </style>
    <script src='https://cdn.jsdelivr.net/npm/chart.js'></script>
</head>
<body>
    <h2>Employee Working Hours Summary</h2>
    <table>
        <tr><th>Employee Name</th><th>Total Working Hours</th></tr>";

            foreach (var entry in groupedData)
            {
                htmlContent += $"<tr><td>{entry.EmployeeName}</td><td>{entry.TotalHours:F2}</td></tr>";
            }

            htmlContent += $@"
    </table>
    <div class='chart'>
        <canvas id='chart'></canvas>
    </div>
    <p><b>Report generated on:</b> {DateTime.Now}</p>
    <script>
        const labels = {labelsJson};
        const data = {valuesJson};
        const ctx = document.getElementById('chart').getContext('2d');
        new Chart(ctx, {{
            type: 'bar',
            data: {{
                labels: labels,
                datasets: [{{
                    label: 'Total Working Hours',
                    data: data,
                    backgroundColor: 'rgba(54, 162, 235, 0.7)',
                    borderColor: 'rgba(54, 162, 235, 1)',
                    borderWidth: 1
                }}]
            }},
            options: {{
                responsive: true,
                scales: {{
                    y: {{
                        beginAtZero: true
                    }}
                }}
            }}
        }});
    </script>
</body>
</html>";

            await File.WriteAllTextAsync("output.html", htmlContent);
            Console.WriteLine("HTML report + chart generated: output.html");

             
             try
             {
                 var htmlPath = Path.Combine(Directory.GetCurrentDirectory(), "output.html");
                 System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                 {
                     FileName = htmlPath,
                     UseShellExecute = true
                 });
             }
             catch
             {
                 Console.WriteLine("Open 'output.html' manually to view the report.");
             }
 
             Console.WriteLine(" All tasks completed!");
         }
         catch (Exception ex)
         {
             Console.WriteLine($" Error: {ex.Message}");
         }
     }
}