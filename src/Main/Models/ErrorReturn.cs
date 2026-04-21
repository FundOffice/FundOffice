using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;

public record ErrorReturn(bool Successed, string? Error);
