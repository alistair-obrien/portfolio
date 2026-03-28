//public interface IItemPlacementInput // Intent
//{
//    void PlacementClicked(int button);
//    void PlacementHovered();
//    void PointerExited();
//}

//public interface IItemPlacementOutput // Rendering
//{
//    void Populate(ItemPlacementViewData viewData);
//    void ClearCellHighlights();
//    void HighlightPlacement(bool valid);
//    void BindInput(IItemPlacementInput coordinator);
//}

//public class ItemPlacementCoordinator : CoordinatorBase, IItemPlacementInput
//{
//    IItemPlacementOutput _output;

//    public void Bind(IItemPlacementOutput output)
//    {
//        _output = output;
//    }

//    public void PlacementClicked(int button)
//    {
//        Debug.Log(":)");
//    }

//    public void PlacementHovered()
//    {
//        _output.HighlightPlacement(true);
//    }

//    public void PointerExited()
//    {
//        _output.ClearCellHighlights();
//    }
//}
