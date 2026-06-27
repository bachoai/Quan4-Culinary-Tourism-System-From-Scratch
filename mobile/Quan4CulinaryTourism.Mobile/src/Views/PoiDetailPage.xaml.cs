using Quan4CulinaryTourism.Mobile.ViewModels;

namespace Quan4CulinaryTourism.Mobile.Views;

public partial class PoiDetailPage : ContentPage, IQueryAttributable
{
    private readonly PoiDetailViewModel _viewModel;
    private string? _poiId;

    public PoiDetailPage(PoiDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _poiId = query.TryGetValue("id", out var id) ? id?.ToString() : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AttachAudioEvents();
        if (!string.IsNullOrWhiteSpace(_poiId))
        {
            await _viewModel.InitializeAsync(_poiId);
        }
    }

    protected override void OnDisappearing()
    {
        _viewModel.DetachAudioEvents();
        base.OnDisappearing();
    }
}
