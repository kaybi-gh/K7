using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class K7CoverPickerDialog
{
    private static readonly MetadataPictureType[] TypeOrder =
    [
        MetadataPictureType.Poster,
        MetadataPictureType.Backdrop,
        MetadataPictureType.Still,
        MetadataPictureType.Cover
    ];

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public List<LibraryPictureDto> Pictures { get; set; } = [];
    [Parameter] public string CancelText { get; set; } = "Cancel";
    [Parameter] public string ConfirmText { get; set; } = "Select";

    private Guid? _selectedSourceId;
    private IBrowserFile? _file;
    private string _searchQuery = "";
    private MetadataPictureType _selectedType = MetadataPictureType.Poster;
    private List<LibraryPictureDto> _pictures = [];
    private List<MetadataPictureType> _pictureTypes = [];
    private Dictionary<MetadataPictureType, int> _typeCounts = [];
    private List<LibraryPictureDto> _filteredPictures = [];

    private bool CanConfirm => _selectedSourceId.HasValue || _file is not null;

    private bool HasPictures => _pictures.Count > 0;

    private int GridItemWidth => _selectedType switch
    {
        MetadataPictureType.Backdrop or MetadataPictureType.Still => 200,
        MetadataPictureType.Cover => 120,
        _ => 100
    };

    private float GridAspectRatio => _selectedType switch
    {
        MetadataPictureType.Backdrop or MetadataPictureType.Still => 9f / 16f,
        MetadataPictureType.Cover => 1f,
        _ => 1.5f
    };

    private string GridItemClass => _selectedType switch
    {
        MetadataPictureType.Backdrop or MetadataPictureType.Still => "cover-picker-item--backdrop",
        MetadataPictureType.Cover => "cover-picker-item--cover",
        _ => "cover-picker-item--poster"
    };

    protected override void OnParametersSet()
    {
        _pictures = Pictures;
        ApplyFilters();
    }

    private void SetTypeFilter(MetadataPictureType type)
    {
        if (_selectedType == type)
            return;

        _selectedType = type;
        ApplyFilters(preserveSelectedType: true);
    }

    private void ApplyFilters() => ApplyFilters(preserveSelectedType: false);

    private void ApplyFilters(bool preserveSelectedType)
    {
        var search = _searchQuery.Trim();
        var searched = string.IsNullOrEmpty(search)
            ? _pictures
            : _pictures
                .Where(p => p.MediaTitle?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

        _pictureTypes = _pictures
            .Select(p => p.Type)
            .Distinct()
            .OrderBy(GetTypeOrder)
            .ToList();

        _typeCounts = _pictureTypes.ToDictionary(
            type => type,
            type => searched.Count(p => p.Type == type));

        if (_pictureTypes.Count == 0)
        {
            _filteredPictures = [];
            return;
        }

        if (!_pictureTypes.Contains(_selectedType))
        {
            _selectedType = _pictureTypes[0];
        }
        else if (!preserveSelectedType && _typeCounts.GetValueOrDefault(_selectedType) == 0)
        {
            var preferred = _pictureTypes.FirstOrDefault(type => _typeCounts.GetValueOrDefault(type) > 0);
            if (preferred != default)
                _selectedType = preferred;
        }

        _filteredPictures = searched
            .Where(p => p.Type == _selectedType)
            .ToList();
    }

    private static int GetTypeOrder(MetadataPictureType type)
    {
        var index = Array.IndexOf(TypeOrder, type);
        return index >= 0 ? index : int.MaxValue;
    }

    private void SelectPicture(Guid pictureId)
    {
        _selectedSourceId = pictureId;
        _file = null;
    }

    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        _file = e.File;
        _selectedSourceId = null;
    }

    private void Confirm()
    {
        var result = _file is not null
            ? new CoverPickerResult { File = _file }
            : new CoverPickerResult { SourcePictureId = _selectedSourceId };
        Dialog.Close(K7DialogResult.Ok(result));
    }

    private void Cancel() => Dialog.Cancel();
}
