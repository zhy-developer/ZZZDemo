
using System;
using System.Collections.Generic;

public class BindableProperty<T>
{
    private T mValue = default(T);

    public Action<T> OnValueChanged;
    // Atomic combat writes publish notifications only after both targets have committed.
    public void SetWithoutNotify(T value) { mValue = value; }
    public void NotifyCurrentValue() { OnValueChanged?.Invoke(mValue); }

    public T Value
    {
        get { return mValue; }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(value, mValue))
            {
                mValue = value;
                OnValueChanged?.Invoke(mValue);
            }
            
        }

    }
}
