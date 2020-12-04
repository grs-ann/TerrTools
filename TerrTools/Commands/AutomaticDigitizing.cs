using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using TerrTools.UI;

namespace TerrTools
{
    [Transaction(TransactionMode.Manual)]
    class AutomaticDigitizing : IExternalCommand
    {
        UIDocument UIDoc { get; set; }
        Document Doc { get => UIDoc.Document; }
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDoc = commandData.Application.ActiveUIDocument;
            var visibleDWGGeometry = GetDWGLines();
            WorkWithDWGLines(visibleDWGGeometry);
            return Result.Succeeded;
        }
        // Возвращает коллекцию линий.
        public List<GeometryObject> GetDWGLines()
        {
            var visibleDWGGeometry = new List<GeometryObject>();
            var pickedDWG = Doc.GetElement(UIDoc.Selection.PickObject(ObjectType.Element));
            var geometry = pickedDWG.get_Geometry(new Options());
            foreach (var item in geometry)
            {
                var geometryInstance = item as GeometryInstance;
                var elementGeometry = geometryInstance.GetInstanceGeometry();
                if (elementGeometry != null)
                {
                    foreach (var geom in elementGeometry)
                    {
                        if (geom is PolyLine)
                        {
                            var geomStyle = Doc.GetElement(geom.GraphicsStyleId) as GraphicsStyle;
                            if (!UIDoc.ActiveView.GetCategoryHidden(geomStyle.GraphicsStyleCategory.Id))
                            {
                                visibleDWGGeometry.Add(geom);
                            }
                        }
                    }
                }
            }
            return visibleDWGGeometry;
        }
        public void WorkWithDWGLines(List<GeometryObject> visibleDWGGeometry)
        {
            using (Transaction trans = new Transaction(Doc))
            {
                trans.Start("name");
                foreach (var item in visibleDWGGeometry)
                {
                    var polyLine = item as Curve;
                    if (polyLine != null)
                    {
                        var curve = Doc.Create.NewModelCurve(polyLine, UIDoc.ActiveView.SketchPlane);
                    }
                }
                trans.Commit();
            }
        }
    }
}
