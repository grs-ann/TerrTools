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
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using TerrTools.UI;

namespace TerrTools
{
    [Transaction(TransactionMode.Manual)]
    class Marking : IExternalCommand
    {
        public UIDocument UIDoc { get; set; }
        public Document Doc { get => UIDoc.Document; }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDoc = commandData.Application.ActiveUIDocument;
                var selectedMarkReference = UIDoc.Selection.PickObject(ObjectType.Element, new TagsSelectionFilter(), "Выберите марку");
                var selectedMark = Doc.GetElement(selectedMarkReference.ElementId);
                MarkingElements(selectedMarkReference, selectedMark);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            
        }
        private IEnumerable<ElementId> GetAllViewInstancesByCategory(ElementId selectedItem)
        {
           var excessItem =  Doc.GetElement(selectedItem);
           var allViewInstanceIds = new FilteredElementCollector(Doc, UIDoc.ActiveView.Id)
                         .OfCategoryId(excessItem.Category.Id)
                         .WhereElementIsNotElementType()
                         .ToElementIds()
                         .Where(x => excessItem.Id != x);
           return allViewInstanceIds;
        }
        // Маркиру ет элементы с учетом свойств выбранной пользователем марки.
        // нужно BIC а не mark
        private void MarkingElements(Reference selectedMarkReference, Element selectedTag)
        {
            using (Transaction trans = new Transaction(Doc))
            {
                trans.Start("Creating new tags for elements");
                // Случай, когда пользователь выбрал марку помещения.
                if ((BuiltInCategory)selectedTag.Category.Id.IntegerValue == BuiltInCategory.OST_RoomTags)
                {
                    var selectedRoomTag = (RoomTag)selectedTag;
                    var instancesIdsForTagging = GetAllViewInstancesByCategory(selectedRoomTag.Room.Id);
                    foreach (var instanceIdForTagging in instancesIdsForTagging)
                    {
                        var tagEndPoint = GetCenterFromBoundingBox(Doc.GetElement(instanceIdForTagging));
                        UV uvPoint = new UV(tagEndPoint.X, tagEndPoint.Y);
                        var createdRoomTag = Doc.Create.NewRoomTag(new LinkElementId(instanceIdForTagging), uvPoint, UIDoc.ActiveView.Id);
                        createdRoomTag.HasLeader = selectedRoomTag.HasLeader;
                        createdRoomTag.TagOrientation = selectedRoomTag.TagOrientation;
                    }
                }
                // Случай, когда пользователь выбрал марку пространства.
                else if ((BuiltInCategory)selectedTag.Category.Id.IntegerValue == BuiltInCategory.OST_MEPSpaceTags)
                {
                    var selectedSpaceTag = (SpaceTag)selectedTag;
                    var instancesIdsForTagging = GetAllViewInstancesByCategory(selectedSpaceTag.Space.Id);
                    foreach (var instanceIdForTagging in instancesIdsForTagging)
                    {
                        var tagEndPoint = GetCenterFromBoundingBox(Doc.GetElement(instanceIdForTagging));
                        UV uvPoint = new UV(tagEndPoint.X, tagEndPoint.Y);
                        var createdSpaceTag = Doc.Create.NewSpaceTag((Space)Doc.GetElement(instanceIdForTagging), uvPoint, UIDoc.ActiveView);
                        createdSpaceTag.TagOrientation = selectedSpaceTag.TagOrientation;
                        createdSpaceTag.HasLeader = selectedSpaceTag.HasLeader;
                    }
                }
                else
                {
                    var independentSelectedTag = (IndependentTag)selectedTag;
                    var instancesIdsForTagging = GetAllViewInstancesByCategory(independentSelectedTag.TaggedLocalElementId);
                    foreach (var instanceIdForTagging in instancesIdsForTagging)
                    {
                        Reference tagHostReference = new Reference(Doc.GetElement(instanceIdForTagging));
                        var tagEndPoint = GetCenterFromBoundingBox(Doc.GetElement(instanceIdForTagging));
                        var createdTag = IndependentTag.Create(Doc, independentSelectedTag.GetTypeId(), UIDoc.ActiveView.Id,
                        tagHostReference, independentSelectedTag.HasLeader, independentSelectedTag.TagOrientation, tagEndPoint);
                        createdTag.LeaderEndCondition = independentSelectedTag.LeaderEndCondition;
                    }
                }
                trans.Commit();
            }
        }
        // Возвращает центр из BoundingBox элемента.
        private XYZ GetCenterFromBoundingBox(Element el)
        {
            var boundingBox = el.get_BoundingBox(null);
            var centerFromBB = (boundingBox.Max + boundingBox.Min) / 2;
            return centerFromBB;
        }
        // Фильтр для выбора элементов. 
        // Позволит пользователю выбрать только из марок.
        private class TagsSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element.Category.IsTagCategory)
                {
                    return true;
                }
                return false;
            }
            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }
        }
    }
  
 
}
