﻿using Grasshopper.Kernel;
using Speckle.Core.Logging;
using Speckle.Core.Transports;
using System;
using System.Drawing;

namespace ConnectorGrasshopper.Transports
{
  public class SqliteTransportComponent : GH_Component
  {
    public override Guid ComponentGuid { get => new Guid("DFFAF45E-06A8-4458-85D8-74FDA8DF3268"); }

    protected override Bitmap Icon => Properties.Resources.SQLiteTransport;

    public override GH_Exposure Exposure => GH_Exposure.primary;

    public SqliteTransportComponent() : base("Sqlite Transport", "Sqlite", "Creates a Sqlite Transport.", "Speckle 2 Dev", "Transports") { }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddTextParameter("base path", "P", "The root folder where you want the sqlite db to be stored. Defaults to `%appdata%`.", GH_ParamAccess.item);
      pManager.AddTextParameter("application name", "N", "The subfolder you want the sqlite db to be stored. Defaults to `Speckle`.", GH_ParamAccess.item);
      pManager.AddTextParameter("database name", "D", "The name of the actual database file. Defaults to `Custom Speckle Sqlite Db`.", GH_ParamAccess.item, "Custom Speckle Sqlite Db");

      Params.Input.ForEach(p => p.Optional = true);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddGenericParameter("sqlite transport", "T", "The Sqlite transport you have created.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      if (DA.Iteration != 0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Cannot create multiple transports at the same time. This is an explicit guard against possibly unintended behaviour. If you want to create another transport, please use a new component.");
        return;
      }

      string basePath = null, applicationName = null, scope = null;

      DA.GetData(0, ref basePath);
      DA.GetData(1, ref applicationName);
      DA.GetData(2, ref scope);

      var myTransport = new SQLiteTransport(basePath, applicationName, scope);

      DA.SetData(0, myTransport);
    }

    protected override void BeforeSolveInstance()
    {
      Tracker.TrackPageview("transports", "sqlite");
      base.BeforeSolveInstance();
    }

  }
}
