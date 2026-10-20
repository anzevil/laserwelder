using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LaserWelderApp.ViewModels;
using LaserWelderApp.Models;
using ScottPlot;

namespace LaserWelderApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _isRendering = false;

    public MainWindow()
    {
        App.Log("MainWindow Initializing...");
        try
        {
            ConfigureScottPlotKoreanFont();

            InitializeComponent();
            App.Log("MainWindow Initialized Successfully.");
        }
        catch (Exception ex)
        {
            App.Log($"[FATAL] MainWindow Init Failed: {ex}");
            throw;
        }

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        App.Log("MainWindow Loaded Event Fired.");
        if (DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _viewModel.RequestChartRefresh += OnRequestChartRefresh;

            // 초기 차트 렌더링
            Dispatcher.InvokeAsync(RenderCharts, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// ViewModel에서 호출되는 이벤트 핸들러 — UI 스레드에서 차트 렌더링
    /// </summary>
    private void OnRequestChartRefresh()
    {
        if (!_isRendering)
        {
            Dispatcher.InvokeAsync(RenderCharts, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private static void ConfigureScottPlotKoreanFont()
    {
        const string fontName = "Malgun Gothic";
        string fontPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Fonts",
            "malgun.ttf");

        if (!System.IO.File.Exists(fontPath))
            throw new System.IO.FileNotFoundException($"Korean font file was not found: {fontPath}", fontPath);

        ScottPlot.Fonts.AddFontFile(fontName, fontPath);
        ScottPlot.Fonts.Default = fontName;
        App.Log($"[FONT] Registered Korean font: {fontPath}");
    }

    private static void ConfigureKoreanPlotFont(ScottPlot.Plot plot)
    {
        plot.Font.Set("Malgun Gothic");
        plot.Legend.FontName = "Malgun Gothic";
        plot.Legend.SetBestFontOnEachRender = true;
    }

    private void RenderCharts()
    {
        if (_isRendering) return;
        _isRendering = true;

        try
        {
            if (_viewModel == null) return;
            var records = _viewModel.WorkRecords.OrderBy(r => r.WorkDate).ThenBy(r => r.WorkTime).ToList();
            if (records.Count == 0)
            {
                App.Log("[WARN] RenderCharts: No records to render.");
                return;
            }

            App.Log($"[INFO] RenderCharts: Rendering {records.Count} records...");
            RenderProductionChart(records);
            RenderProcessChart(records);
            RenderQualityChart(records);
            App.Log("[INFO] RenderCharts: Done.");
        }
        catch (Exception ex)
        {
            App.Log($"[ERROR] RenderCharts failed: {ex}");
        }
        finally
        {
            _isRendering = false;
        }
    }

    /// <summary>
    /// 차트 1: 일자별 총 생산 수량 및 작업 시간
    /// </summary>
    private void RenderProductionChart(List<WorkRecord> records)
    {
        // 완전 초기화 (기존 범례 포함 모두 제거)
        WpfPlotProduction.Reset();
        var plot = WpfPlotProduction.Plot;

        // 일자별 그룹화
        var dailyGroups = records.GroupBy(r => r.WorkDate).OrderBy(g => g.Key).ToList();
        int count = dailyGroups.Count;

        double[] xs = new double[count];
        double[] quantities = new double[count];
        double[] durations = new double[count];
        Tick[] ticks = new Tick[count];

        for (int i = 0; i < count; i++)
        {
            xs[i] = i;
            quantities[i] = dailyGroups[i].Sum(x => x.TotalQuantity);
            durations[i] = dailyGroups[i].Sum(x => x.WorkDuration);

            // 날짜 레이블 (MM/dd 형식)
            string label = dailyGroups[i].Key.Length >= 5 ? dailyGroups[i].Key.Substring(5) : dailyGroups[i].Key;
            ticks[i] = new Tick(i, label);
        }

        // ScottPlot 5 Bars 추가
        var bars = new List<Bar>();
        for (int i = 0; i < count; i++)
        {
            bars.Add(new Bar
            {
                Position = xs[i],
                Value = quantities[i],
                FillColor = new Color(2, 132, 199), // SkyBlue
                LineColor = new Color(3, 105, 161),
                LineWidth = 1.5f
            });
        }
        var barPlot = plot.Add.Bars(bars);
        barPlot.LegendText = "생산량(개)";

        // 작업 시간 꺾은선 (Scatter)
        var scatter = plot.Add.Scatter(xs, durations);
        scatter.Color = new Color(16, 185, 129); // Green
        scatter.LineWidth = 2.5f;
        scatter.MarkerSize = 7f;
        scatter.LegendText = "작업 시간(분)";

        // 가로축 날짜 틱
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        plot.YLabel("생산량(개) / 시간(분)");

        ConfigureKoreanPlotFont(plot);

        plot.ShowLegend(Edge.Top);
        plot.Axes.AutoScale();
        WpfPlotProduction.Refresh();
    }

    /// <summary>
    /// 차트 2: 공정 조건 (스캔 속도 &amp; 레이저 출력 시계열 이력)
    /// </summary>
    private void RenderProcessChart(List<WorkRecord> records)
    {
        // 완전 초기화
        WpfPlotProcess.Reset();
        var plot = WpfPlotProcess.Plot;

        int n = records.Count;
        double[] xs = new double[n];
        double[] speeds = new double[n];
        double[] powers = new double[n];

        for (int i = 0; i < n; i++)
        {
            xs[i] = i + 1;
            speeds[i] = records[i].ScanSpeed;
            powers[i] = records[i].MaxPower / 5.0;
        }

        var speedScatter = plot.Add.Scatter(xs, speeds);
        speedScatter.Color = new Color(245, 158, 11); // Amber
        speedScatter.LineWidth = 2f;
        speedScatter.MarkerSize = 6f;
        speedScatter.LegendText = "스캔 속도 (mm/s)";

        var powerScatter = plot.Add.Scatter(xs, powers);
        powerScatter.Color = new Color(220, 38, 38); // Red
        powerScatter.LineWidth = 2f;
        powerScatter.MarkerSize = 6f;
        powerScatter.LegendText = "출력 (W/5)";

        plot.XLabel("작업 차수 (#)");
        plot.YLabel("속도(mm/s) 및 출력 환산치");

        ConfigureKoreanPlotFont(plot);

        plot.ShowLegend(Edge.Top);
        plot.Axes.AutoScale();
        WpfPlotProcess.Refresh();
    }

    /// <summary>
    /// 차트 3: 비드 폭 품질 관리 차트 (기준 4.0mm ± 0.2mm 관리 한계선)
    /// </summary>
    private void RenderQualityChart(List<WorkRecord> records)
    {
        // 완전 초기화
        WpfPlotQuality.Reset();
        var plot = WpfPlotQuality.Plot;

        int n = records.Count;
        double[] xs = new double[n];
        double[] beadWidths = new double[n];

        for (int i = 0; i < n; i++)
        {
            xs[i] = i + 1;
            beadWidths[i] = records[i].MeasuredBeadWidth;
        }

        // 실측 비드 폭
        var scatter = plot.Add.Scatter(xs, beadWidths);
        scatter.Color = new Color(99, 102, 241); // Indigo
        scatter.LineWidth = 2f;
        scatter.MarkerSize = 7f;
        scatter.LegendText = "실측 비드 폭 (mm)";

        // 기준선 (4.0mm) & 관리 상한/하한선 (4.2mm / 3.8mm)
        var centerLine = plot.Add.HorizontalLine(4.0);
        centerLine.Color = new Color(34, 197, 94); // Green
        centerLine.LinePattern = LinePattern.Dashed;
        centerLine.LineWidth = 1.5f;

        var upperLine = plot.Add.HorizontalLine(4.2);
        upperLine.Color = new Color(239, 68, 68); // Red
        upperLine.LinePattern = LinePattern.Dotted;
        upperLine.LineWidth = 1.5f;

        var lowerLine = plot.Add.HorizontalLine(3.8);
        lowerLine.Color = new Color(239, 68, 68); // Red
        lowerLine.LinePattern = LinePattern.Dotted;
        lowerLine.LineWidth = 1.5f;

        plot.XLabel("작업 차수 (#)");
        plot.YLabel("비드 폭 (mm)");
        plot.Axes.SetLimitsY(3.6, 4.4);

        ConfigureKoreanPlotFont(plot);

        plot.ShowLegend(Edge.Top);
        WpfPlotQuality.Refresh();
    }
}