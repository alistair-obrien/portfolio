public static class GridHelper
{
    private static bool OverlapsFootprint(
        int testX, int testY,
        int objX, int objY,
        int objW, int objH)
    {
        return
            testX >= objX &&
            testY >= objY &&
            testX < objX + objW &&
            testY < objY + objH;
    }
}