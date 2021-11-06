using System;

namespace llsc
{
  public class SharedValue<T> where T : IComparable<T>
  {
    public T Value;

    public SharedValue(T value) => this.Value = value;
  }
}
