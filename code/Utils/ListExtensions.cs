using System;

public static class ListExtensions
{
    extension<T>(List<T> list) where T : class
    {
        public T GetRandom()
        {
            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];

            return list[Random.Shared.Int(list.Count - 1)];
        }
    }
}