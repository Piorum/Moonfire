using Moonfire.Rendering.Enums;
using Moonfire.Rendering.Models;

namespace Moonfire.Rendering.Interfaces;

/// <summary>
/// View that can request a width/height and render itself within assigned bounds.
/// </summary>
public interface IRelativeMoonfireView : IMoonfireView
{
    /// <summary>
    /// Relative X position, as compared to other views
    /// </summary>
    int X { get; }
    /// <summary>
    /// Relative Y position, as compared to other views
    /// </summary>
    int Y { get; }
    /// <summary>
    /// Relative Z position, as compared to other views
    /// </summary>
    int Z { get; }

    /// <summary>
    /// Determines the way in which the view's width is calculated.
    /// </summary>
    FillStyle FillStyleX { get; }
    /// <summary>
    /// Determines the way in which the view's height is calculated.
    /// </summary>
    FillStyle FillStyleY { get; }
    /// <summary>
    /// Requested static width in cells, only check if FillStyleX is FillStyle.Static.
    /// </summary>
    int StaticX { get; }
    /// <summary>
    /// Requested static height in cells, only check if FillStyleY is FillStyle.Static.
    /// </summary>
    int StaticY { get; }
    /// <summary>
    /// Requested percentage of width for the view where 1.0f is equal to 100%.
    /// </summary>
    float PercentX { get; } //1.0f == 100%
    /// <summary>
    /// Requested percentage of height for the view where 1.0f is equal to 100%.
    /// </summary>
    float PercentY { get; } //1.0f == 100%
    int MinX { get; }
    int MinY { get; }
}

/// <summary>
/// View that has no way to request width or height but can render if given them.
/// </summary>
public interface IMoonfireView
{
    /// <summary>
    /// Left most absolute position of the view.
    /// </summary>
    int OriginX { set; get; }
    /// <summary>
    /// Top most absolute position of the view.
    /// </summary>
    int OriginY { set; get; }
    /// <summary>
    /// Absolute width of the view.
    /// </summary>
    int SizeX { set; get; }
    /// <summary>
    /// Absolute height of the view.
    /// </summary>
    int SizeY { set; get; }

    /// <summary>
    /// Code that will be called when container view is done arranging.
    /// Should arrange subviews NOT itself then propogate to subviews.
    /// </summary>
    /// <returns>True if work done, otherwise False</returns>
    Task<bool> Arrange();

    /// <summary>
    /// Code that will be called when container view is done drawing.
    /// Should draw itself then propogate subviews.
    /// </summary>
    /// <param name="context">Context in which view lives wherein (0,0) is equal to (View.OriginX,View.OriginY)</param>
    Task Draw(TerminalContext context);

    /// <summary>
    /// Code that will be called when the container view is dirtied.
    /// Should dirty itself then propogate to subviews.
    /// </summary>
    Task Invalidate();
}
