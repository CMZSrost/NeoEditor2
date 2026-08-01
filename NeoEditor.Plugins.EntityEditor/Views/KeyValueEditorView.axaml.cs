using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Command;
using NeoEditor.Data.Messages;
using NeoEditor.Plugins.EntityEditor.ViewModels;

namespace NeoEditor.Plugins.EntityEditor.Views;

public partial class KeyValueEditorView : UserControl
{
    private KeyValueEditorViewModel? _vm;
    private ObservableCollection<FieldSection>? _subscribedSections;

    private static T GetService<T>() where T : notnull
        => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

    public KeyValueEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
                UnsubscribeFields(_subscribedSections);
            }
            _vm = DataContext as KeyValueEditorViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                SubscribeToSections(_vm.Sections);
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyValueEditorViewModel.Sections) && _vm != null)
            SubscribeToSections(_vm.Sections);
    }

    private void SubscribeToSections(ObservableCollection<FieldSection>? sections)
    {
        UnsubscribeFields(_subscribedSections);
        _subscribedSections = sections;
        if (sections != null)
        {
            sections.CollectionChanged += OnSectionsChanged;
            SubscribeFieldRows();
        }
    }

    private void UnsubscribeFields(ObservableCollection<FieldSection>? sections)
    {
        if (sections == null) return;
        sections.CollectionChanged -= OnSectionsChanged;
        foreach (var section in sections)
        foreach (var field in section.Fields)
            field.PropertyChanged -= OnFieldPropertyChanged;
    }

    private void OnSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SubscribeFieldRows();

    private void SubscribeFieldRows()
    {
        if (_vm == null) return;
        foreach (var section in _vm.Sections)
        foreach (var field in section.Fields)
        {
            field.PropertyChanged -= OnFieldPropertyChanged;
            field.PropertyChanged += OnFieldPropertyChanged;
        }
    }

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FieldRow field || e.PropertyName != nameof(FieldRow.CurrentValue)) return;
        if (_vm?.CurrentEntity == null || field.Property == null || field.IsKey) return;

        try
        {
            var converted = ValueConverter.Convert(field.CurrentValue, field.PropertyType);
            field.Property.SetValue(_vm.CurrentEntity, converted);
        }
        catch { /* conversion failed — wait for focus loss to retry */ }
    }

    private void OnValueBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_vm?.CurrentEntity == null) return;
        FieldRow? field = null;
        if (sender is Control ctrl) field = ctrl.DataContext as FieldRow;
        if (field == null) return;
        if (field.Property == null || field.IsKey) return;

        object? newTypedValue = null;
        try
        {
            newTypedValue = ValueConverter.Convert(field.CurrentValue, field.PropertyType);
            field.Property.SetValue(_vm.CurrentEntity, newTypedValue);
        }
        catch { /* ignore unparseable value */ }

        try
        {
            var oldTypedValue = ValueConverter.Convert(field.OriginalValue, field.PropertyType);
            if (oldTypedValue != null && newTypedValue != null && !Equals(oldTypedValue, newTypedValue))
            {
                var editRecord = new EditRecord(
                    _vm.CurrentEntity, field.Property, field.PropertyName,
                    oldTypedValue, newTypedValue);
                WeakReferenceMessenger.Default.Send(
                    new EntityFieldEditsMessage(_vm.CurrentEntity, new System.Collections.Generic.List<EditRecord> { editRecord }));
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[KV-LostFocus] WAL persist ERROR: {ex.Message}"); }

        field.OriginalValue = field.CurrentValue;
        field.IsDirty = false;

        WeakReferenceMessenger.Default.Send(
            new RefreshEntityEditorMessage(_vm.CurrentEntity));
    }

    private void OnValueBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            OnValueBoxLostFocus(sender, e);
            e.Handled = true;
        }
    }
}
