// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace QuickWindows.Settings;

public sealed class SettingItem<T>(T startValue) : INotifyPropertyChanged
{
    public T Value
    {
        get => startValue;
        set
        {
            startValue = value;
            OnValueChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnValueChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
