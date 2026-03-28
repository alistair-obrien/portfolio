using System;

public class MultipleLineTextAttribute : Attribute
{
    internal int Lines;

    public MultipleLineTextAttribute(int defaultLines)
    {
        Lines = defaultLines;
    }
}
