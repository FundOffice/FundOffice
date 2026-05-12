using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;

public record ErrorReturn(bool Successed, string? Error = null);

public record Return<T>(bool Successed, T Data, string? Message = null);