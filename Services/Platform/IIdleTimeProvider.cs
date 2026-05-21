using System;

namespace firstProject.Services.Platform;

public interface IIdleTimeProvider
{
    TimeSpan GetIdleTime();
}
