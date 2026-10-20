using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LaserWelderApp.Models;

/// <summary>
/// 레이저 용접기 프리셋 파라미터 데이터 모델
/// </summary>
public class WeldingPreset : INotifyPropertyChanged
{
    private int _presetId;
    private string _presetName = string.Empty;
    private double _scanSpeed;
    private double _scanWidth;
    private double _maxPower;
    private double _dutyCycle;
    private double _frequency;
    private DateTime _updatedAt;
    private bool _isSelected;

    public int PresetId
    {
        get => _presetId;
        set { _presetId = value; OnPropertyChanged(); }
    }

    public string PresetName
    {
        get => _presetName;
        set { _presetName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 스캔 속도 (mm/s)
    /// </summary>
    public double ScanSpeed
    {
        get => _scanSpeed;
        set { _scanSpeed = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 스캔 폭 (mm)
    /// </summary>
    public double ScanWidth
    {
        get => _scanWidth;
        set { _scanWidth = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 최대 출력 (W)
    /// </summary>
    public double MaxPower
    {
        get => _maxPower;
        set { _maxPower = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 사용률 (%)
    /// </summary>
    public double DutyCycle
    {
        get => _dutyCycle;
        set { _dutyCycle = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 주파수 (Hz)
    /// </summary>
    public double Frequency
    {
        get => _frequency;
        set { _frequency = value; OnPropertyChanged(); }
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set { _updatedAt = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public WeldingPreset Clone()
    {
        return new WeldingPreset
        {
            PresetId = this.PresetId,
            PresetName = this.PresetName,
            ScanSpeed = this.ScanSpeed,
            ScanWidth = this.ScanWidth,
            MaxPower = this.MaxPower,
            DutyCycle = this.DutyCycle,
            Frequency = this.Frequency,
            UpdatedAt = this.UpdatedAt,
            IsSelected = this.IsSelected
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
