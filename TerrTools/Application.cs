using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Diagnostics;
using TerrTools.Updaters;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.DB.Events;
namespace TerrTools
{
    public class App : IExternalApplication
    {
        public static UIControlledApplication Application { get; set; }
        public static List<TerrUpdater> Updaters = new List<TerrUpdater>();
        public static AddInId AddInId { get => new AddInId(new Guid("4e6830af-73c4-45fa-aea0-82df352d5157")); }
        public static string AppName { get => "ТеррНИИ BIM"; }
        public static string DLLPath { get => @"\\serverL\PSD\REVIT\Плагины\TerrTools\TerrToolsDLL\TerrTools.dll";  }
        public static string UpdaterPath { get => @"\\serverL\PSD\REVIT\Плагины\TerrTools\TerrToolsUpdater.exe";  }
        public static string Version { get => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion; }
        public static string PatchNote { get => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).Comments; }
        
        int btnCounter = 0;
        int pullBtnCounter = 0;

        static public bool CheckUpdates(out string lastReleaseVersion, out string patchNote)
        {
            try
            {
                lastReleaseVersion = FileVersionInfo.GetVersionInfo(DLLPath).FileVersion;
                patchNote = FileVersionInfo.GetVersionInfo(DLLPath).Comments;
                return App.Version != lastReleaseVersion;
            }
            catch
            {
                lastReleaseVersion = patchNote = "0";
                return false;
            }
        }

        private ImageSource BitmapToImageSource(string embeddedPath)
        {
            try
            {
                Stream stream = GetType().Assembly.GetManifestResourceStream(embeddedPath);
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                return decoder.Frames[0];
            }
            catch (ArgumentNullException)
            {
                return null;
            }
        }

        private PushButtonData MakePushButton (string className, string btnText, string toolTip = null, string iconName = null)
        {
            btnCounter++;
            PushButtonData btnData = new PushButtonData(
                "PushButton" + btnCounter.ToString(),
                btnText,
                Assembly.GetExecutingAssembly().Location,
                "TerrTools." + className);
            btnData.ToolTip = toolTip ?? "";
            if (iconName != null) btnData.LargeImage = BitmapToImageSource("TerrTools.Resources.Icons." + iconName);
            return btnData;
        }

        private PulldownButtonData MakePulldownButton (string btnText, string toolTip = null, string iconName = null)
        {
            pullBtnCounter++;
            PulldownButtonData btnData = new PulldownButtonData("PulldownButton" + pullBtnCounter.ToString(), btnText);
            btnData.ToolTip = toolTip ?? "";
            if (iconName != null) btnData.LargeImage = BitmapToImageSource("TerrTools.Resources.Icons." + iconName);
            return btnData;
        }

        private void InitUpdaters()
        {            
            ChangeType ChangeTypeAdditionAndModication = ChangeType.ConcatenateChangeTypes(Element.GetChangeTypeAny(), Element.GetChangeTypeElementAddition());
            ChangeType allChangeTypes = ChangeType.ConcatenateChangeTypes(ChangeTypeAdditionAndModication, Element.GetChangeTypeElementDeletion());
            SharedParameterSettings settings;

            // SpaceUpdater
            Updaters.Add(new SpaceUpdater(                 
                new ElementCategoryFilter(BuiltInCategory.OST_MEPSpaces),
                ChangeTypeAdditionAndModication));

            // DuctUpdater
            var upd = new DuctsUpdater(new LogicalAndFilter(new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves), 
                                                            new ElementIsElementTypeFilter(true)),
                                       ChangeTypeAdditionAndModication);            
            upd.AddSharedSettings(new SharedParameterSettings(BuiltInCategory.OST_DuctCurves, "ADSK_Толщина стенки"));
            upd.AddSharedSettings(new SharedParameterSettings(BuiltInCategory.OST_DuctCurves, "ТеррНИИ_Класс герметичности"));
            upd.AddSharedSettings(new SharedParameterSettings(BuiltInCategory.OST_DuctCurves, "ТеррНИИ_Отметка от нуля"));
            upd.AddSharedSettings(new SharedParameterSettings(BuiltInCategory.OST_DuctCurves, "ТеррНИИ_Горизонтальный воздуховод"));
            upd.AddSharedSettings(new SharedParameterSettings(BuiltInCategory.OST_DuctCurves, "ТеррНИИ_Вертикальный воздуховод"));
            Updaters.Add(upd);

            // DuctsAccessoryUpdater
            Updaters.Add(new DuctsAccessoryUpdater(                
                new LogicalAndFilter(new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory), new ElementIsElementTypeFilter(true)),
                ChangeTypeAdditionAndModication));

            // PartUpdater
            Updaters.Add(new PartUpdater(                
                new ElementCategoryFilter(BuiltInCategory.OST_Parts),
                Element.GetChangeTypeAny()));

            // SystemNamingUpdater
            BuiltInCategory[] elemCats = new BuiltInCategory[]
            {
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PlaceHolderDucts,
                BuiltInCategory.OST_DuctTerminal,
                BuiltInCategory.OST_FlexDuctCurves,
                BuiltInCategory.OST_FlexPipeCurves,
                BuiltInCategory.OST_DuctInsulations,
                BuiltInCategory.OST_PipeInsulations,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PlaceHolderPipes,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_Sprinklers,
                BuiltInCategory.OST_PlumbingFixtures
            };
            BuiltInCategory[] sysCats = new BuiltInCategory[]
            {
                BuiltInCategory.OST_DuctSystem,
                BuiltInCategory.OST_PipingSystem,
            };
            var elemFilter = new LogicalAndFilter(new ElementMulticategoryFilter(elemCats), new ElementIsElementTypeFilter(inverted: true));
            var sysFilter = new LogicalAndFilter(new ElementMulticategoryFilter(sysCats), new ElementIsElementTypeFilter(inverted: true));
            SystemNamingUpdater updater = new SystemNamingUpdater(elemFilter, ChangeTypeAdditionAndModication, elemCats, sysCats);          
            settings = new SharedParameterSettings(elemCats.Concat(sysCats).ToArray(),
                                                                            "ТеррНИИ_Наименование системы",
                                                                            BuiltInParameterGroup.PG_TEXT);
            updater.AddSharedSettings(settings);
            updater.AddTriggerPair(sysFilter, ChangeTypeAdditionAndModication);
            Updaters.Add(updater);

            // RoomUpdater
            var filterRDW = new ElementMulticategoryFilter(new BuiltInCategory[]{
            BuiltInCategory.OST_Rooms, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows });
            var filterDW = new ElementMulticategoryFilter(new BuiltInCategory[]{
            BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows });
            Updaters.Add(new RoomUpdater(                
                filterRDW,
                ChangeTypeAdditionAndModication));
            Updaters.Last().AddTriggerPair(filterDW, Element.GetChangeTypeElementDeletion());
        }

        private void AddUpdaterSharedParameters(Document doc)
        {
            using (Transaction tr = new Transaction(doc, "Добавление общих параметров ТеррНИИ BIM"))
            {
                tr.Start();
                foreach (TerrUpdater upd in Updaters)
                {
                    foreach (SharedParameterSettings s in upd.SharedParameterSettings)
                    {
                        SharedParameterUtils.AddSharedParameter(doc, s);
                    }
                }
                tr.Commit();
            }
        }

        private void RegisterUpdaters()
        {
            foreach (var upd in Updaters)
            {
                UpdaterRegistry.RegisterUpdater(upd);
                foreach (var trigger in upd.TriggerPairs)
                {
                    UpdaterRegistry.AddTrigger(upd.GetUpdaterId(), trigger.Filter, trigger.ChangeType);
                }
            }
        }

        private void RegisterUpdaters(Document doc)
        {
            foreach (var upd in Updaters)
            {
                UpdaterRegistry.RegisterUpdater(upd, doc);
                foreach (var trigger in upd.TriggerPairs)
                {
                    UpdaterRegistry.AddTrigger(upd.GetUpdaterId(), trigger.Filter, trigger.ChangeType);
                }
            }
        }

        private void UnregisterUpdaters()
        {
            foreach (IUpdater upd in Updaters) UpdaterRegistry.UnregisterUpdater(upd.GetUpdaterId());
        }
        private void UnregisterUpdaters(Document doc)
        {
            foreach (IUpdater upd in Updaters) UpdaterRegistry.UnregisterUpdater(upd.GetUpdaterId(), doc);
        }

        private void CreateRibbon()
        {
            Application.CreateRibbonTab(AppName);
            RibbonPanel panelInfo = Application.CreateRibbonPanel(AppName, "ТеррНИИ BIM");
            RibbonPanel panelArch = Application.CreateRibbonPanel(AppName, "АР");
            RibbonPanel panelStruct = Application.CreateRibbonPanel(AppName, "КР");
            RibbonPanel panelMEP = Application.CreateRibbonPanel(AppName, "ОВиК");
            RibbonPanel panelGeneral = Application.CreateRibbonPanel(AppName, "Общие");

            Dictionary<string, PushButtonData> pbDict = new Dictionary<string, PushButtonData>();
            Dictionary<string, PulldownButtonData> plDict = new Dictionary<string, PulldownButtonData>();

            PulldownButton tempBtn;

            ///
            /// Push buttons
            ///            
            pbDict.Add("DiffuserProcessing",
            MakePushButton(
                "DiffuserProcessing",
                "Обновить\nдиффузоры",
                "Копирует номер пространства и расход воздуха для всех диффузоров в проекте",
                "Diffuser.png"
                ));
            pbDict.Add("RadiatorProcessing",
                MakePushButton(
                "RadiatorProcessing",
                "Обновить\nрадиаторы",
                "Копирует номер пространства и подбирает радиатор исходя из заданной в пространстве тепловой мощности",
                "Radiator.png"));
            pbDict.Add("WallOpening",
           MakePushButton(
                "WallOpeningHandler",
                "Генерация\nотверстий",
                "Вставляет отверстия в местах пересечений с системами",
                "Openings.png"
                ));
            pbDict.Add("GenerateFloor",
           MakePushButton(
                "FloorFinishing",
                "Отделка\nпола",
                "Создает элемент \"Перекрытие\" нужного типоразмера в указанных помещениях",
                "Brush.png"
                ));
            pbDict.Add("FocusOnElement",
                MakePushButton(
                    "FocusOnElement",
                    "Поиск элементов\nна виде",
                    "Выберите из списка необходимый элемент и окно сфокусируется на нем",
                    "Zoom.png"
                    ));
            pbDict.Add("UpdateTypeCurrent",
                MakePushButton(
                    "TypeChanger",
                    "В текущем проекте"
                    ));
            pbDict.Add("UpdateTypeAll",
                MakePushButton(
                    "TypeChangerDeep",
                    "В проекте и семействах"
                    ));
            pbDict.Add("IzometryGenerator",
                MakePushButton(
                    "IzometryGenerator",
                    "Создать 3D виды\nпо системам",
                    "Генерирует 3D-виды с фильтрами по системам",
                    "3D.png"
                    ));

            pbDict.Add("CopyRoomShape",
                MakePushButton(
                    "CopyRoomShape",
                    "Создать контур\nпомещения",
                    iconName:"Shape.png"
                    ));            

            pbDict.Add("SystemScheduleExporter",
                MakePushButton(
                    "ScheduleExporter",
                    "Экспорт\nспецификаций",
                    iconName: "Tables.png"
                    ));
            pbDict.Add("PythonExecuter",
                MakePushButton(
                    "PythonExecuter",
                    "Запуск скрипта",
                    iconName: "Python.png",
                    toolTip: "Позволяет запускать файлы скриптов с форматов .py"
                    ));
            pbDict.Add("ColumnFinish",
                MakePushButton(
                    "ColumnFinish",
                    "Отделка\nколонн",
                    iconName: "Column.png",
                    toolTip: "Автоматически генерирует штукатурку для всех колонн, находящихся в помещениях"
                    ));
            pbDict.Add("GridAxes",
                MakePushButton(
                    "GridAxes",
                    "Создать сетку\nосей",
                    iconName: "Grids.png",
                    toolTip: "Создает сетку осей с заданным шагом"
                    ));

            pbDict.Add("CollisionViewer",
                MakePushButton(
                    "CollisionViewer",
                    "Просмотр\nколлизий",
                    iconName: "Goal.png",
                    toolTip: "Просмотр отчета о коллизиях в отдельном окне"
                    ));

            pbDict.Add("InsulCurvesDocument",
                MakePushButton(
                    "InsulCurvesDocument",
                    "Во всем документе"
                    ));

            pbDict.Add("InsulCurvesView",
                MakePushButton(
                    "InsulCurvesView",
                    "На текущем виде"
                    ));

            pbDict.Add("InsulCurvesSelection",
                MakePushButton(
                    "InsulCurvesSelection",
                    "Выбрать вручную"
                    ));
            pbDict.Add("Marking",
                MakePushButton(
                    "Marking",
                    "Маркировать по\n выбранной марке",
                    iconName: "Marking.png"
                    ));


            ///
            /// Pulldown buttons
            ///
            plDict.Add("UpdateType",
                MakePulldownButton(
                    "Обновить\nшрифт",
                    "Обновление всех шрифтов в проекте под стандарты предприятия",
                    "Type.png"
                    ));

            plDict.Add("InsulCurves",
                MakePulldownButton(
                    "3D маркировка\nизоляции",
                    iconName: "Insul.png"
                    ));

            ///
            /// Архитектурная панель
            ///
            panelArch.AddItem(pbDict["GenerateFloor"]);
            panelArch.AddItem(pbDict["ColumnFinish"]);

            ///
            /// Конструкторская панель
            ///
            panelStruct.AddItem(pbDict["WallOpening"]);

            ///
            /// ОВиК панель
            ///
            panelMEP.AddItem(pbDict["DiffuserProcessing"]);
            panelMEP.AddItem(pbDict["RadiatorProcessing"]);
            panelMEP.AddItem(pbDict["IzometryGenerator"]);

            tempBtn = panelMEP.AddItem(plDict["InsulCurves"]) as PulldownButton;
            tempBtn.AddPushButton(pbDict["InsulCurvesDocument"]);
            tempBtn.AddPushButton(pbDict["InsulCurvesView"]);
            tempBtn.AddPushButton(pbDict["InsulCurvesSelection"]);


            ///
            /// Общая панель
            ///
            panelGeneral.AddItem(pbDict["FocusOnElement"]);
            panelGeneral.AddItem(pbDict["CollisionViewer"]);

            tempBtn = panelGeneral.AddItem(plDict["UpdateType"]) as PulldownButton;
            tempBtn.AddPushButton(pbDict["UpdateTypeCurrent"]);
            tempBtn.AddPushButton(pbDict["UpdateTypeAll"]);

            panelGeneral.AddItem(pbDict["SystemScheduleExporter"]);
            panelGeneral.AddItem(pbDict["GridAxes"]);
            panelGeneral.AddItem(pbDict["CopyRoomShape"]);
            panelGeneral.AddItem(pbDict["PythonExecuter"]);
            panelGeneral.AddItem(pbDict["Marking"]);


            ///
            /// Настройки
            ///
            panelInfo.AddItem(MakePushButton("SettingsWindow", "Настройки", iconName: "Settings.png"));

#if DEBUG
            panelInfo.AddItem(MakePushButton("DebuggingTools", "DEBUG",
                toolTip: "Если по какой-то причине эта кнопка осталось в релизной версии и вы не знаете, что она делает - НЕ НАЖИМАЙТЕ"
                    ));
#endif
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            UnregisterUpdaters();
            return Result.Succeeded;
        }
        
        public Result OnStartup(UIControlledApplication app)
        {          
            App.Application = app;
            CheckUpdateDialog();            
            CreateRibbon();
            InitUpdaters();
            RegisterEvents();            
            return Result.Succeeded;
        }

        private void RegisterEvents()
        {
            // удаление срабатывает через раз, а кроме него ничего не переписано сейчас
            // так что убираем до лучших времен
            //Application.Idling += OverrideCommands;
            Application.ControlledApplication.FailuresProcessing += Application_FailureProcessing;
            Application.ControlledApplication.DocumentOpened += Application_DocumentOpened;
            Application.ControlledApplication.DocumentCreated += Application_DocumentCreated;
            Application.ControlledApplication.DocumentChanged += Application_DocumentChanged;
        }

        private void Application_DocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            SystemNamingUpdater.FirstExecutionInTransaction = true;
        }

        private void Application_DocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            Document doc = e.Document;
            GlobalVariables.CurrentDocument = doc;
            AddUpdaterSharedParameters(doc);
            RegisterUpdaters(doc);
        }
        private void Application_DocumentCreated(object sender, DocumentCreatedEventArgs e)
        {
            Document doc = e.Document;
            GlobalVariables.CurrentDocument = doc;
            AddUpdaterSharedParameters(doc);
            RegisterUpdaters(doc);
        }


        /// <summary>
        /// Обрабатывает возникающие ошибки во время транзакции
        /// В Revit выглядит как окно предупреждения. Правильная настройка позволит убрать
        /// надоедливые предупреждения
        /// </summary>
        private void Application_FailureProcessing(object sender, FailuresProcessingEventArgs e)
        {
            FailuresAccessor fa = e.GetFailuresAccessor();
            IList<FailureMessageAccessor>  failList = fa.GetFailureMessages(); // Inside event handler, get all warnings
            foreach (FailureMessageAccessor failure in failList)
            {
                // check FailureDefinitionIds against ones that you want to dismiss, FailureDefinitionId failID = failure.GetFailureDefinitionId();
                // prevent Revit from showing Unenclosed room warnings
                FailureDefinitionId failID = failure.GetFailureDefinitionId();
                if (failID == BuiltInFailures.GeneralFailures.DuplicateValue)
                {
                    fa.DeleteWarning(failure);
                }
            }
        }

        // В этом методе можно перезаписать поведение стандартных комманд в Revit
        // Список команд можно найти в файле commandids.txt в папке проекта
        private void OverrideCommands(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            Application.Idling -= OverrideCommands;
            UIApplication uiapp = sender as UIApplication;
            if (uiapp != null)
            {
                
                RevitCommandId commandId = RevitCommandId.LookupCommandId("ID_BUTTON_DELETE");
                try
                {
                    AddInCommandBinding deleteBinding = uiapp.CreateAddInCommandBinding(commandId);
                    deleteBinding.Executed += new EventHandler<Autodesk.Revit.UI.Events.ExecutedEventArgs>(DeleteBinding_Executed);
                }
                catch
                {
                }
            }
        }

        private void DeleteBinding_Executed(object sender, ExecutedEventArgs e)
        {
            UIApplication uiapp = sender as UIApplication;            
            Document doc = uiapp.ActiveUIDocument.Document;
            var ids = uiapp.ActiveUIDocument.Selection.GetElementIds();
            using (Transaction tr = new Transaction(doc, "Удаление элементов"))
            {
                tr.Start();
                foreach (var id in ids)
                {
                    try
                    {
                        BuiltInCategory cat = (BuiltInCategory)doc.GetElement(id).Category.Id.IntegerValue;
                        switch (cat)
                        {
                            case BuiltInCategory.OST_Views:
                                ViewDeletingProcess(doc, id);
                                break;
                            default:
                                doc.Delete(id);
                                break;
                        }
                    }
                    catch
                    {
                        doc.Delete(id);
                    }
                }
                tr.Commit();
            }
        }
        
        private void ViewDeletingProcess(Document doc, ElementId id)
        {
            View view = doc.GetElement(id) as View;
            ElementId sheetId;            
            if (view is ViewSchedule)
            {
                var col = new FilteredElementCollector(doc)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>()
                    .Where(x=>x.ScheduleId.IntegerValue == id.IntegerValue);
                sheetId = col.Count() > 0 ? col.First().OwnerViewId : null;
            }
            else
            {
                var col = new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Where(x => x.ViewId.IntegerValue == id.IntegerValue);
                sheetId = col.Count() > 0 ? col.First().OwnerViewId : null;
            }
            if (sheetId != null)
            {
                var td = new TaskDialog("Удаление");
                td.MainInstruction = string.Format(
                    "Удаляемый вид {0} расположен на листе {1}. После удаления он пропадает с листа, а отменить удаление будет невозможно. Вы уверены?",
                    doc.GetElement(id).Name, doc.GetElement(sheetId).Name);
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Удалить");
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Отмена");
                var res = td.Show();
                if (res == TaskDialogResult.CommandLink1) doc.Delete(id);                
            }
            else
            {
                doc.Delete(id);
            }
        }


        static public void CheckUpdateDialog(bool showIfActual=false)
        {
            if (CheckUpdates(out string lastReleaseVersion, out string patchNote))
            {
                TaskDialog td = new TaskDialog("Доступно обновление");
                td.MainInstruction = "На сервере доступна новая версия плагина. РЕКОМЕНДУЕТСЯ обновить плагин прямо сейчас";
                td.MainContent = string.Format("Текущая версия: {0}\nДоступная версия: {1}\n\nЧто нового: \n{2}", App.Version, lastReleaseVersion, patchNote);
                td.FooterText = "Вы можете обновить плагин до последней версии в любое время, запустив файл " + UpdaterPath;
                td.AllowCancellation = false;
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Закрыть Revit и обновить");
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Продолжить без обновления");

                if (td.Show() == TaskDialogResult.CommandLink1)
                {
                    var revitProcess = System.Diagnostics.Process.GetCurrentProcess();
                    Process.Start(UpdaterPath);
                    revitProcess.Kill();
                }
            }
            else if (showIfActual)
            {
                TaskDialog td = new TaskDialog("ТеррНИИ BIM");
                td.MainInstruction = "У вас установлена актуальная версия плагина";
                td.Show();
            }
        }


        public static void StartUpdaterService(string argLine)
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = @"cmd";
            psi.Arguments = "/C start " + UpdaterPath + " " + argLine;
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            if (File.Exists(UpdaterPath)) Process.Start(psi);
            else TaskDialog.Show("Ошибка", "Программа обновления отсутствует на сервере. Обновитесь самостоятельно");
        }
    }


    [Transaction(TransactionMode.Manual)]
    public class SettingsWindow : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var form = new UI.SettingsForm(commandData.Application.ActiveUIDocument.Document);
            return Result.Succeeded;
        }
    }    
}
