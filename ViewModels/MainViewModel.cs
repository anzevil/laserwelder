using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LaserWelderApp.Models;
using LaserWelderApp.Services;

namespace LaserWelderApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly DatabaseService _dbService;

    // 1. 용접 파라미터 필드
    private double _scanSpeed = 200.0;
    private double _scanWidth = 4.00;
    private double _maxPower = 1000.0;
    private double _dutyCycle = 100.0;
    private double _frequency = 3000.0;

    private int _selectedPresetId = 8;
    private string _statusMessage = "시스템 준비 완료";
    private string _dbLocation = string.Empty;
    private bool _isLoading;

    // 2. 탭 선택 필드 (0: 파라미터 설정, 1: 작업 실적 입력, 2: 차트 통계)
    private int _selectedTabIndex = 0;

    // 3. 작업 실적 신규 입력 필드
    private int _inputDuration = 45;
    private int _inputTotalQuantity = 100;
    private int _inputDefectQuantity = 1;
    private double _inputGasFlow = 15.0;
    private double _inputMeasuredBeadWidth = 4.05;
    private string _inputOperatorName = "작업자A";

    // 4. 통계 요약 지표
    private int _totalProductionCount;
    private int _totalWorkDurationMinutes;
    private double _averageDefectRate;
    private int _totalRecordCount;

    public ObservableCollection<WeldingPreset> Presets { get; } = new();
    public ObservableCollection<WorkRecord> WorkRecords { get; } = new();

    public event Action? RequestChartRefresh;

    #region 파라미터 프로퍼티
    public double ScanSpeed
    {
        get => _scanSpeed;
        set => SetProperty(ref _scanSpeed, value);
    }

    public double ScanWidth
    {
        get => _scanWidth;
        set => SetProperty(ref _scanWidth, value);
    }

    public double MaxPower
    {
        get => _maxPower;
        set => SetProperty(ref _maxPower, value);
    }

    public double DutyCycle
    {
        get => _dutyCycle;
        set => SetProperty(ref _dutyCycle, value);
    }

    public double Frequency
    {
        get => _frequency;
        set => SetProperty(ref _frequency, value);
    }

    public int SelectedPresetId
    {
        get => _selectedPresetId;
        set
        {
            if (SetProperty(ref _selectedPresetId, value))
            {
                UpdateSelectionState();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string DbLocation
    {
        get => _dbLocation;
        set => SetProperty(ref _dbLocation, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    #endregion

    #region 네비게이션 및 작업 실적 프로퍼티
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                if (_selectedTabIndex == 2)
                {
                    RequestChartRefresh?.Invoke();
                }
            }
        }
    }

    public int InputDuration
    {
        get => _inputDuration;
        set => SetProperty(ref _inputDuration, value);
    }

    public int InputTotalQuantity
    {
        get => _inputTotalQuantity;
        set => SetProperty(ref _inputTotalQuantity, value);
    }

    public int InputDefectQuantity
    {
        get => _inputDefectQuantity;
        set => SetProperty(ref _inputDefectQuantity, value);
    }

    public double InputGasFlow
    {
        get => _inputGasFlow;
        set => SetProperty(ref _inputGasFlow, value);
    }

    public double InputMeasuredBeadWidth
    {
        get => _inputMeasuredBeadWidth;
        set => SetProperty(ref _inputMeasuredBeadWidth, value);
    }

    public string InputOperatorName
    {
        get => _inputOperatorName;
        set => SetProperty(ref _inputOperatorName, value);
    }

    public int TotalProductionCount
    {
        get => _totalProductionCount;
        set => SetProperty(ref _totalProductionCount, value);
    }

    public int TotalWorkDurationMinutes
    {
        get => _totalWorkDurationMinutes;
        set => SetProperty(ref _totalWorkDurationMinutes, value);
    }

    public double AverageDefectRate
    {
        get => _averageDefectRate;
        set => SetProperty(ref _averageDefectRate, value);
    }

    public int TotalRecordCount
    {
        get => _totalRecordCount;
        set => SetProperty(ref _totalRecordCount, value);
    }
    #endregion

    #region 커맨드
    public ICommand SelectPresetCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand HelpCommand { get; }

    public ICommand SaveWorkRecordCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand RefreshChartCommand { get; }
    #endregion

    public MainViewModel()
    {
        _dbService = new DatabaseService();
        DbLocation = _dbService.DatabasePath;

        SelectPresetCommand = new AsyncRelayCommand(async param =>
        {
            if (param is int id)
            {
                await SelectPresetAsync(id);
            }
            else if (int.TryParse(param?.ToString(), out int parsedId))
            {
                await SelectPresetAsync(parsedId);
            }
        });

        LoadCommand = new AsyncRelayCommand(LoadCurrentPresetAsync);
        SaveCommand = new AsyncRelayCommand(SaveCurrentPresetAsync);
        ResetCommand = new AsyncRelayCommand(ResetCurrentInputsAsync);
        HelpCommand = new RelayCommand(ShowHelpDialog);

        SaveWorkRecordCommand = new AsyncRelayCommand(SaveWorkRecordAsync);
        SwitchTabCommand = new RelayCommand(param =>
        {
            if (param is int tabIdx) SelectedTabIndex = tabIdx;
            else if (int.TryParse(param?.ToString(), out int parsedIdx)) SelectedTabIndex = parsedIdx;
        });
        RefreshChartCommand = new AsyncRelayCommand(async () => await ReloadWorkRecordsAsync());

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "데이터베이스 초기화 및 데이터 로딩 중...";
            await _dbService.InitializeDatabaseAsync();

            // 1. 프리셋 로드
            var presetList = await _dbService.GetAllPresetsAsync();
            Presets.Clear();
            foreach (var preset in presetList)
            {
                preset.IsSelected = (preset.PresetId == SelectedPresetId);
                Presets.Add(preset);
            }

            await SelectPresetAsync(SelectedPresetId);

            // 2. 작업 실적 이력 로드
            await ReloadWorkRecordsAsync();

            StatusMessage = "데이터베이스 연동 완료 (SQLite)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"DB 초기화 오류: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadWorkRecordsAsync()
    {
        var records = await _dbService.GetAllWorkRecordsAsync();
        WorkRecords.Clear();
        foreach (var r in records.OrderByDescending(x => x.RecordId))
        {
            WorkRecords.Add(r);
        }

        // 요약 통계 계산
        if (records.Any())
        {
            TotalProductionCount = records.Sum(r => r.TotalQuantity);
            TotalWorkDurationMinutes = records.Sum(r => r.WorkDuration);
            int totalDefects = records.Sum(r => r.DefectQuantity);
            AverageDefectRate = TotalProductionCount > 0 
                ? Math.Round((double)totalDefects / TotalProductionCount * 100.0, 2) 
                : 0.0;
            TotalRecordCount = records.Count;
        }

        RequestChartRefresh?.Invoke();
    }

    private async Task SaveWorkRecordAsync()
    {
        try
        {
            if (InputDuration <= 0)
            {
                MessageBox.Show("작업 시간을 1분 이상 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (InputTotalQuantity <= 0)
            {
                MessageBox.Show("총 수량을 1개 이상 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (InputDefectQuantity < 0 || InputDefectQuantity > InputTotalQuantity)
            {
                MessageBox.Show("불량 수량이 올바르지 않습니다 (0 <= 불량 수량 <= 총 수량).", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            var record = new WorkRecord
            {
                WorkDate = DateTime.Now.ToString("yyyy-MM-dd"),
                WorkTime = DateTime.Now.ToString("HH:mm:ss"),
                PresetId = SelectedPresetId,
                ScanSpeed = ScanSpeed,
                MaxPower = MaxPower,
                WorkDuration = InputDuration,
                TotalQuantity = InputTotalQuantity,
                DefectQuantity = InputDefectQuantity,
                GasFlow = InputGasFlow,
                MeasuredBeadWidth = InputMeasuredBeadWidth,
                OperatorName = string.IsNullOrWhiteSpace(InputOperatorName) ? "작업자" : InputOperatorName.Trim()
            };

            await _dbService.AddWorkRecordAsync(record);
            await ReloadWorkRecordsAsync();

            StatusMessage = $"[등록 완료] {record.WorkDate} {record.WorkTime} 작업 실적이 DB에 성공적으로 등록되었습니다.";
            MessageBox.Show($"작업 실적이 성공적으로 저장되었습니다!\n\n- 일시: {record.FullDateTimeString}\n- 생산 수량: {record.TotalQuantity}개 (불량 {record.DefectQuantity}개)\n- 작업 시간: {record.WorkDuration}분\n- 통계 차트 화면에서 바로 확인하실 수 있습니다.",
                "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);

            // 차트 탭으로 자동 이동 안내
            SelectedTabIndex = 2;
        }
        catch (Exception ex)
        {
            StatusMessage = $"작업 실적 저장 오류: {ex.Message}";
            MessageBox.Show($"저장 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SelectPresetAsync(int presetId)
    {
        SelectedPresetId = presetId;
        UpdateSelectionState();

        var preset = await _dbService.GetPresetAsync(presetId);
        if (preset != null)
        {
            ScanSpeed = preset.ScanSpeed;
            ScanWidth = preset.ScanWidth;
            MaxPower = preset.MaxPower;
            DutyCycle = preset.DutyCycle;
            Frequency = preset.Frequency;
            StatusMessage = $"[{presetId}번 - {preset.PresetName}] 프리셋 불러옴";
        }
    }

    private void UpdateSelectionState()
    {
        foreach (var p in Presets)
        {
            p.IsSelected = (p.PresetId == SelectedPresetId);
        }
    }

    private async Task LoadCurrentPresetAsync()
    {
        try
        {
            IsLoading = true;
            await SelectPresetAsync(SelectedPresetId);
            StatusMessage = $"{SelectedPresetId}번 프리셋을 DB에서 새로 불러왔습니다.";
            await _dbService.LogActionAsync(SelectedPresetId, "LOAD", "사용자 버튼에 의한 불러옴");
        }
        catch (Exception ex)
        {
            StatusMessage = $"불러오기 오류: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveCurrentPresetAsync()
    {
        try
        {
            if (ScanSpeed <= 0 || ScanWidth < 0 || MaxPower <= 0 || DutyCycle < 0 || DutyCycle > 100 || Frequency <= 0)
            {
                MessageBox.Show("입력값을 확인해주세요.\n\n- 스캔 속도 > 0\n- 스캔 폭 >= 0\n- 최대 출력 > 0\n- 사용률: 0 ~ 100%\n- 주파수 > 0", 
                    "입력값 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            var targetPreset = Presets.FirstOrDefault(p => p.PresetId == SelectedPresetId);
            string name = targetPreset != null ? targetPreset.PresetName : "기능 설정";

            var preset = new WeldingPreset
            {
                PresetId = SelectedPresetId,
                PresetName = name,
                ScanSpeed = ScanSpeed,
                ScanWidth = ScanWidth,
                MaxPower = MaxPower,
                DutyCycle = DutyCycle,
                Frequency = Frequency,
                UpdatedAt = DateTime.Now
            };

            await _dbService.SavePresetAsync(preset);

            if (targetPreset != null)
            {
                targetPreset.ScanSpeed = ScanSpeed;
                targetPreset.ScanWidth = ScanWidth;
                targetPreset.MaxPower = MaxPower;
                targetPreset.DutyCycle = DutyCycle;
                targetPreset.Frequency = Frequency;
                targetPreset.UpdatedAt = preset.UpdatedAt;
            }

            StatusMessage = $"[성공] {SelectedPresetId}번 슬롯의 용접 조건이 DB에 저장되었습니다.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 오류: {ex.Message}";
            MessageBox.Show($"DB 저장 중 오류 발생:\n{ex.Message}", "저장 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResetCurrentInputsAsync()
    {
        var preset = await _dbService.GetPresetAsync(SelectedPresetId);
        if (preset != null)
        {
            ScanSpeed = preset.ScanSpeed;
            ScanWidth = preset.ScanWidth;
            MaxPower = preset.MaxPower;
            DutyCycle = preset.DutyCycle;
            Frequency = preset.Frequency;
            StatusMessage = $"{SelectedPresetId}번 슬롯의 저장된 값으로 되돌렸습니다.";
        }
    }

    private void ShowHelpDialog()
    {
        string helpText = @"[ 레이저 용접 시스템 파라미터 및 실적 관리 가이드 ]

1. 파라미터 설정 (기존 화면)
   - 스캔 속도, 스캔 폭, 최대 출력, 사용률, 주파수 조작 및 1~9번 프리셋 저장

2. 작업 실적 입력 (신규 화면)
   - 작업자가 용접 작업을 마친 후 '작업 시간(분)', '생산 수량', '불량 수량', '가스 유량', '실측 비드 폭'을 수기로 등록합니다.
   - 현재 설정된 스캔 속도와 레이저 출력이 자동으로 함께 기록됩니다.

3. 통계 및 시계열 차트 (신규 화면)
   - [차트 1] 일별 총 생산 수량 & 작업 시간 추이
   - [차트 2] 스캔 속도 및 레이저 출력 시계열 공정 이력
   - [차트 3] 실측 비드 폭 품질 관리도 (기준 폭 4.0mm 대비 공차 확인)
   - 마우스 휠로 줌(Zoom), 드래그로 이동(Pan)이 가능합니다.";

        MessageBox.Show(helpText, "도움말 - 레이저 용접 시스템", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
