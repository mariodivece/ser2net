namespace Unosquare.Ser2Net.Workers;


/// <summary>
/// Interface for a <see cref="BackgroundService"/>
/// that has one or more child <see cref="BackgroundService"/>.
/// </summary>
internal interface IParentBackgroundService
{

    /// <summary>
    /// Gets the registered children background services.
    /// </summary>
    IReadOnlyList<BackgroundService> Children { get; }
}
