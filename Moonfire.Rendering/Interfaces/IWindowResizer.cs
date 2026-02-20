namespace Moonfire.Rendering.Interfaces;

public interface IWindowResizer
{
    Task RegisterResizeEvent(Renderer root);
    bool Registered { get; }
}