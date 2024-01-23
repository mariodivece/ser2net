using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Ser2Net.Workers;

internal interface IParentBackgroundService
{
    IReadOnlyList<BackgroundService> Children { get; }
}
