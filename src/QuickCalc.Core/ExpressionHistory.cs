namespace QuickCalc.Core;

public sealed class ExpressionHistory(int capacity = 50)
{
    private readonly List<string> _items = [];
    private int _cursor;
    public IReadOnlyList<string> Items => _items;

    public void Add(string expression)
    {
        expression = expression.Trim();
        if (expression.Length == 0) return;
        _items.Remove(expression);
        _items.Add(expression);
        if (_items.Count > capacity) _items.RemoveAt(0);
        _cursor = _items.Count;
    }

    public string? Previous()
    {
        if (_items.Count == 0) return null;
        _cursor = Math.Max(0, _cursor - 1);
        return _items[_cursor];
    }

    public string? Next()
    {
        if (_items.Count == 0) return null;
        _cursor = Math.Min(_items.Count, _cursor + 1);
        return _cursor == _items.Count ? "" : _items[_cursor];
    }

    public void ResetNavigation() => _cursor = _items.Count;
    public void Clear() { _items.Clear(); _cursor = 0; }
}
