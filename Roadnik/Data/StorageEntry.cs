﻿using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Data;

[SimpleDocument("geo-data")]
internal record StorageEntry(
  float Latitude,
  float Longitude,
  float Altitude,
  float? Speed = null,
  float? Accuracy = null,
  float? Battery = null,
  float? GsmSignal = null,
  float? Bearing = null,
  string? Message = null);