﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using SpeckleCore;
using SpeckleElementsClasses;

namespace SpeckleElementsRevit
{
  public static partial class Conversions
  {

    public static Grid ToNative( this GridLine myGridLine )
    {
      var (docObj, stateObj) = GetExistingElementByApplicationId( myGridLine.ApplicationId, myGridLine.Type );

      // If no doc object, means we need to create it!
      if( docObj == null )
      {
        var res = Grid.Create( Doc, Autodesk.Revit.DB.Line.CreateBound( new XYZ( myGridLine.Value[ 0 ] * Scale, myGridLine.Value[ 1 ] * Scale, 0 ), new XYZ( myGridLine.Value[ 3 ] * Scale, myGridLine.Value[ 4 ] * Scale, 0 ) ) );
        return res;
      }

      // if the new and old have the same id (hash equivalent) and the doc obj is not marked as being modified, return the doc object
      if( docObj != null && myGridLine._id == stateObj._id && (bool) stateObj.Properties[ "userModified" ] == false )
      {
        return (Grid) docObj;
      }

      // Otherwise, enter "edit" mode: means the doc object has been modfied, or the original source object changed.
      var myGrid = docObj as Grid;
      var oldStart = myGrid.Curve.GetEndPoint( 0 );
      var oldEnd = myGrid.Curve.GetEndPoint( 1 );

      var newStart = new XYZ( myGridLine.Value[ 0 ] * Scale, myGridLine.Value[ 1 ] * Scale, 0 );
      //var newStart = new XYZ( myGridLine.Value[ 0 ] * Scale, myGridLine.Value[ 1 ] * Scale, myGridLine.Value[ 2 ] * Scale );
      var newEnd = new XYZ( myGridLine.Value[ 3 ] * Scale, myGridLine.Value[ 4 ] * Scale, 0 );
      //var newEnd = new XYZ( myGridLine.Value[ 3 ] * Scale, myGridLine.Value[ 4 ] * Scale, myGridLine.Value[ 5 ] * Scale );

      var translate = newStart.Subtract( oldStart );
      ElementTransformUtils.MoveElement( Doc, myGrid.Id, translate );

      var currentDirection = myGrid.Curve.GetEndPoint( 0 ).Subtract( myGrid.Curve.GetEndPoint( 1 ) ).Normalize();
      var newDirection = newStart.Subtract( newEnd ).Normalize();

      var angle = newDirection.AngleTo( currentDirection );

      if( angle > 0.00001 )
      {
        var crossProd = newDirection.CrossProduct( currentDirection ).Z;
        ElementTransformUtils.RotateElement( Doc, myGrid.Id, Autodesk.Revit.DB.Line.CreateUnbound( newStart, XYZ.BasisZ ), crossProd < 0 ? angle : -angle );
      }

      try
      {
        myGrid.SetCurveInView( DatumExtentType.Model, Doc.ActiveView, Autodesk.Revit.DB.Line.CreateBound( newStart, newEnd ) );
      }
      catch( Exception e )
      {
        System.Diagnostics.Debug.WriteLine( "Failed to set grid endpoints." );
      }

      return myGrid;

    }

    // TODO: Create a proper method, this is just fun.
    // TODO: Add parameters (if any? do grids have parameters?) 
    public static GridLine ToSpeckle( this Grid myGrid )
    {
      var start = myGrid.Curve.GetEndPoint( 0 );
      var end = myGrid.Curve.GetEndPoint( 1 );

      var myGridLine = new GridLine()
      {
        ApplicationId = myGrid.UniqueId,
        elementId = myGrid.Id.ToString(),
      Value = new List<double>() { start.X / Scale, start.Y / Scale, start.Z / Scale, end.X / Scale, end.Y / Scale, end.Z / Scale },
        parameters = GetElementParams( myGrid )
      };

      myGridLine.ApplicationId = myGrid.UniqueId;
      myGridLine.GenerateHash();
      return myGridLine;
    }

  }
}

