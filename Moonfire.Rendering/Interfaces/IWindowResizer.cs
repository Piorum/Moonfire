namespace Moonfire.Rendering.Interfaces;

public interface IWindowResizer
{
    void RegisterResizeEvent(Renderer root);
    bool Registered { get; }
}