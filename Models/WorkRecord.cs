using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LaserWelderApp.Models;

/// <summary>
/// 작업자가 수기 입력하는 용접 작업 실적 및 품질 데이터 모델
/// </summary>
public class WorkRecord : INotifyPropertyChanged
{
    private int _recordId;
    private string _workDate = DateTime.Now.ToString("yyyy-MM-dd");
    private string _workTime = DateTime.Now.ToString("HH:mm:ss");
    private int _presetId = 8;
    private double _scanSpeed = 200.0;
    private double _maxPower = 1000.0;
    private int _workDuration = 45;      // 작업 시간 (분)
    private int _totalQuantity = 100;     // 총 생산 수량 (개)
    private int _defectQuantity = 2;      // 불량 수량 (개)
    private double _gasFlow = 15.0;       // 보호 가스 유량 (L/min)
    private double _measuredBeadWidth = 4.05; // 실측 비드 폭 (mm)
    private string _operatorName = "작업자A";

    public int RecordId
    {
        get => _recordId;
        set { _recordId = value; OnPropertyChanged(); }
    }

    public string WorkDate
    {
        get => _workDate;
        set { _workDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(FullDateTimeString)); }
    }

    public string WorkTime
    {
        get => _workTime;
        set { _workTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(FullDateTimeString)); }
    }

    public string FullDateTimeString => $"{WorkDate} {WorkTime}";

    public int PresetId
    {
        get => _presetId;
        set { _presetId = value; OnPropertyChanged(); }
    }

    public double ScanSpeed
    {
        get => _scanSpeed;
        set { _scanSpeed = value; OnPropertyChanged(); }
    }

    public double MaxPower
    {
        get => _maxPower;
        set { _maxPower = value; OnPropertyChanged(); }
    }

    public int WorkDuration
    {
        get => _workDuration;
        set { _workDuration = value; OnPropertyChanged(); }
    }

    public int TotalQuantity
    {
        get => _totalQuantity;
        set { _totalQuantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(GoodQuantity)); OnPropertyChanged(nameof(DefectRate)); }
    }

    public int DefectQuantity
    {
        get => _defectQuantity;
        set { _defectQuantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(GoodQuantity)); OnPropertyChanged(nameof(DefectRate)); }
    }

    public int GoodQuantity => Math.Max(0, TotalQuantity - DefectQuantity);

    public double DefectRate => TotalQuantity > 0 ? Math.Round((double)DefectQuantity / TotalQuantity * 100.0, 1) : 0.0;

    public double GasFlow
    {
        get => _gasFlow;
        set { _gasFlow = value; OnPropertyChanged(); }
    }

    public double MeasuredBeadWidth
    {
        get => _measuredBeadWidth;
        set { _measuredBeadWidth = value; OnPropertyChanged(); }
    }

    public string OperatorName
    {
        get => _operatorName;
        set { _operatorName = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
