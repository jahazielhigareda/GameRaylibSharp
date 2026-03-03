using Client.Services;

namespace Client.ECS.Systems;

/// <summary>
/// TASK-031 – Audio system (Task 9.1).
///
/// Pumps the background music stream every frame and exposes
/// <see cref="AudioService"/> to the rest of the ECS via dependency injection.
/// </summary>
public class AudioSystem : ISystem
{
    private readonly AudioService _audio;

    public AudioSystem(AudioService audio) => _audio = audio;

    public void Update(float deltaTime) => _audio.Update();
}
