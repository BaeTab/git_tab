using System.Collections.ObjectModel;
using GitTab.Core.Gitignore;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitTab.App.ViewModels;

public sealed partial class TemplateChoiceViewModel : ObservableObject
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    [ObservableProperty] private bool _isSelected;
}

/// <summary>Backs the .gitignore generator dialog: pick stacks, preview, write/append.</summary>
public sealed partial class GitignoreDialogViewModel : ObservableObject
{
    private readonly IGitignoreService _service;
    private readonly string _workingDir;

    public GitignoreDialogViewModel(IGitignoreService service, string workingDir)
    {
        _service = service;
        _workingDir = workingDir;

        var detected = new HashSet<string>(service.Detect(workingDir), StringComparer.Ordinal);
        HasExisting = service.ReadExisting(workingDir) is not null;

        Choices = new ObservableCollection<TemplateChoiceViewModel>(
            service.Templates.Select(t =>
            {
                var choice = new TemplateChoiceViewModel { Key = t.Key, Name = t.Name, IsSelected = detected.Contains(t.Key) };
                choice.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TemplateChoiceViewModel.IsSelected))
                        OnPropertyChanged(nameof(PreviewText));
                };
                return choice;
            }));
    }

    public ObservableCollection<TemplateChoiceViewModel> Choices { get; }
    public bool HasExisting { get; }
    public bool Written { get; private set; }

    public string PreviewText => _service.Build(Choices.Where(c => c.IsSelected).Select(c => c.Key));

    public void Apply(bool append)
    {
        var content = _service.Build(Choices.Where(c => c.IsSelected).Select(c => c.Key));
        _service.Write(_workingDir, content, append);
        Written = true;
    }
}
