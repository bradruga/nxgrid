namespace NxGrid.Demo.Shared.Data;

public record SalesRow(
    string Name, string Department,
    int Jan, int Feb, int Mar, int Apr, int May, int Jun,
    int Jul, int Aug, int Sep, int Oct, int Nov, int Dec)
{
    public int Total => Jan + Feb + Mar + Apr + May + Jun + Jul + Aug + Sep + Oct + Nov + Dec;
}
