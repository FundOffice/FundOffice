using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace FMO
{

    [AutoChangeableViewModel(typeof(DateRange))]
    public partial class DateRangeViewModel
    {
        public override string ToString()
        {
            return $"{Begin:yyyy-MM-dd} - {End:yyyy-MM-dd}";
        }
    }


    public partial class SetupFlowViewModel : FlowViewModel, IChangeableEntityViewModel//, IFileSetter
    {

        /// <summary>
        /// 募集开始日期
        /// </summary>
        public ChangeableViewModel<SetupFlow, DateOnly?> RaisingStartDate { get; set; }

        /// <summary>
        /// 募集结束日期
        /// </summary>
        public ChangeableViewModel<SetupFlow, DateOnly?> RaisingEndDate { get; set; }


        public ChangeableViewModel<SetupFlow, DateRangeViewModel?> RaisingPeriod { get; set; }




        public ChangeableViewModel<SetupFlow, decimal?> InitialAsset { get; }


        public string? Capital => InitialAsset.NewValue is null ? null : NumberHelper.NumberToChinese(InitialAsset.NewValue.Value);


        /// <summary>
        /// 实缴出资
        /// </summary>
        public SimpleFileViewModel PaidInCapitalProof { get; }

        /// <summary>
        /// 成立公告
        /// </summary>  
        public DualFileViewModel EstablishmentAnnouncement { get; }






        [SetsRequiredMembers]
        public SetupFlowViewModel(SetupFlow flow) : base(flow)
        {
            RaisingStartDate = new ChangeableViewModel<SetupFlow, DateOnly?>
            {
                InitFunc = x => x.RaisingStartDate == default ? null : x.RaisingStartDate,
                UpdateFunc = (x, y) => x.RaisingStartDate = y ?? default,
                ClearFunc = x => x.RaisingStartDate = default
            };
            RaisingStartDate.Init(flow);

            RaisingEndDate = new ChangeableViewModel<SetupFlow, DateOnly?>
            {
                InitFunc = x => x.RaisingEndDate == default ? null : x.RaisingEndDate,
                UpdateFunc = (x, y) => x.RaisingEndDate = y ?? default,
                ClearFunc = x => x.RaisingEndDate = default
            };
            RaisingEndDate.Init(flow);


            RaisingPeriod = new ChangeableViewModel<SetupFlow, DateRangeViewModel?>
            {
                Label = "募集期",
                InitFunc = x => x.RasingPeriod == default(DateRange) ? new() : new(x.RasingPeriod),
                UpdateFunc = (x, y) => x.RasingPeriod = y!.Build(),
                ClearFunc = x => x.RasingPeriod = default,
            };
            RaisingPeriod.Init(flow);



            InitialAsset = new ChangeableViewModel<SetupFlow, decimal?>
            {
                Label = "募集规模",
                InitFunc = x => x.InitialAsset <= 0 ? null : x.InitialAsset,
                UpdateFunc = (x, y) => x.InitialAsset = y ?? 0,
                ClearFunc = x => x.InitialAsset = 0,
                DisplayFunc = x => $"{x / 10000:N0} 万元"
            };
            InitialAsset.Init(flow);
            InitialAsset.PropertyChanged += (s, e) => { if (e.PropertyName == "NewValue") OnPropertyChanged(nameof(Capital)); };

            //募集规模为0时，检查ta
            if (flow.InitialAsset == 0)
            {
                using var db = DbHelper.Base();
                var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == FundId && x.Type == TransferRecordType.Subscription).ToArray();
                if (ta.Length > 0)
                    InitialAsset.NewValue = ta.Sum(x => x.ConfirmedNetAmount);
            }

            PaidInCapitalProof = new(flow.PaidInCapitalProof) { Filter = "文档|*.docx;*.doc;*.pdf" };
            PaidInCapitalProof.FileChanged += f => SaveFileChanged(new { PaidInCapitalProof = f });


            EstablishmentAnnouncement = new(flow.EstablishmentAnnouncement) { Filter = "文档|*.docx;*.doc;*.pdf" };
            EstablishmentAnnouncement.FileChanged += f => SaveFileChanged(new { EstablishmentAnnouncement = f });


            Initialized = true;
        }








        [RelayCommand]
        public void Delete(IPropertyModifier unit)
        {
            if (unit is IEntityModifier<SetupFlow> entity)
            {
                using var db = DbHelper.Base();
                var v = db.GetCollection<FundFlow>().FindById(FlowId) as SetupFlow;

                if (v is not null)
                {
                    entity.RemoveValue(v);
                    entity.Init(v);
                    db.GetCollection<FundFlow>().Update(v);

                    WeakReferenceMessenger.Default.Send(v);
                }
            }
        }


        [RelayCommand]
        public void Reset(IPropertyModifier unit)
        {
            unit.Reset();
        }



        [RelayCommand]
        public void Modify(IPropertyModifier unit)
        {
            if (unit is IEntityModifier<SetupFlow> property)
            {
                using var db = DbHelper.Base();
                var v = db.GetCollection<FundFlow>().FindById(FlowId) as SetupFlow;

                property.UpdateEntity(v!);
                db.GetCollection<FundFlow>().Update(v!);
            }
            unit.Apply();
        }


        [RelayCommand]
        public void Save()
        {
            var ps = GetType().GetProperties();
            foreach (var p in ps)
            {
                if (p.PropertyType.IsAssignableTo(typeof(IPropertyModifier)) && p.GetValue(this) is IPropertyModifier v && v.IsValueChanged)
                    Modify(v);
            }
            IsReadOnly = true;
        }



        [RelayCommand]
        public void GenerateFile(DualFileMetaViewModel v)
        {
            if (v == EstablishmentAnnouncement)
            {
                string path = Path.GetTempFileName();
                try
                {
                    using var db = DbHelper.Base();
                    var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
                    var fund = db.GetCollection<Fund>().FindById(FundId);

                    var data = new Dictionary<string, object?>
                        {
                            {"Manager", manager.Name },
                            {"Name", fund.Name },
                            {"Date", $"{Date?? DateTime.Today:yyyy年MM月dd日}" },
                            {"Amount", InitialAsset.OldValue },
                            {"Capital", Capital },
                            { "Share", InitialAsset.OldValue }
                        };

                    if (Tpl.GenerateByPredefined(path, "产品成立公告.docx", data))
                        v.Normal.Meta = FileMeta.Create(path, @$"{fund.Name}_产品成立公告.docx");
                    else HandyControl.Controls.Growl.Error($"生成【产品成立公告】失败，请查看Log，检查模板是否存在");
                }
                catch { }
                File.Delete(path);
            }

        }



        //[RelayCommand]
        //public void ChooseFile(SimpleFile<SetupFlow> file)
        //{
        //    var fd = new OpenFileDialog();
        //    fd.Filter = file.Filter;
        //    if (fd.ShowDialog() != true)
        //        return;

        //    SetFile(file, fd.FileName);
        //}


        //public void SetFile(ISimpleFile? file, string path)
        //{
        //    if (file is SimpleFile<SetupFlow> ff)
        //    {
        //        ff.File = new FileInfo(path);

        //        using var db = DbHelper.Base();
        //        var flow = db.GetCollection<FundFlow>().FindById(FlowId) as SetupFlow;
        //        if (flow is SetupFlow f)
        //        {
        //            ff.SetProperty(flow, ff.Build());
        //            db.GetCollection<FundFlow>().Update(flow);
        //        }
        //    }
        //}




        //[RelayCommand]
        //public void Clear(SimpleFile<SetupFlow> file)
        //{
        //    if (file is null) return;

        //    var r = HandyControl.Controls.MessageBox.Show("是否删除文件", "提示", MessageBoxButton.YesNoCancel);
        //    if (r == MessageBoxResult.Cancel) return;

        //    if (r == MessageBoxResult.Yes) file.File?.Delete();

        //    using var db = DbHelper.Base();
        //    var flow = db.GetCollection<FundFlow>().FindById(FlowId) as SetupFlow;
        //    file.SetProperty(flow!, null);
        //    db.GetCollection<FundFlow>().Update(flow!);
        //    file.File = null;
        //}
    }
}