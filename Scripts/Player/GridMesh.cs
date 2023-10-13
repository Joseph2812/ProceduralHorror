using Godot;
using System;

namespace Scripts.Player;

public partial class GridMesh : PrimitiveMesh
{
    private readonly Godot.Collections.Array _arrays; // One surface, therefore only one set of arrays

    public GridMesh(Vector2I size, float space, float thickness)
    {
        if (size.X <= 0 || size.Y <= 0) { throw new ArgumentOutOfRangeException(nameof(size)     , "Size of the grid must be > 0 for both X and Y."); }
        if (space <= 0f)                { throw new ArgumentOutOfRangeException(nameof(space)    , "Space must be > 0."); }
        if (thickness <= 0f)            { throw new ArgumentOutOfRangeException(nameof(thickness), "Thickness must be > 0."); }

        SurfaceTool st = new();
        st.Begin(PrimitiveType.Triangles);
     
        float spaceAndThickness = space + thickness;
        float height = (size.Y * space) + ((size.Y + 1) * thickness);

        // Height-sized Edges For The Far-most Left & Right Side //
        Vector3 toHeightUp = Vector3.Up * height;
        Vector3 toThicknessRight = Vector3.Right * thickness;

        AddHeightEdge(st, spaceAndThickness, toHeightUp, toThicknessRight, 0);
        AddHeightEdge(st, spaceAndThickness, toHeightUp, toThicknessRight, size.X);

        // Vertical Edges //
        Vector3 toSpaceUp = Vector3.Up * space;
        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 1; x < size.X; x++)
            {
                Vector3 bottomLeftPos = new(x * spaceAndThickness, (y * spaceAndThickness) + thickness, 0f);
                Vector3 topLeftPos = bottomLeftPos + toSpaceUp;
                Vector3 bottomRightPos = bottomLeftPos + toThicknessRight;

                // Bottom-left Triangle //
                st.AddVertex(bottomLeftPos);
                st.AddVertex(topLeftPos);
                st.AddVertex(bottomRightPos);

                // Top-right Triangle //
                st.AddVertex(topLeftPos + toThicknessRight);
                st.AddVertex(bottomRightPos);
                st.AddVertex(topLeftPos);
            }
        }

        // Horizontal Edges //
        Vector3 toThicknessUp = Vector3.Up * thickness;
        Vector3 toRowEnd = Vector3.Right * (((size.X - 1) * spaceAndThickness) + space);
        for (int y = 0; y <= size.Y; y++)
        {
            Vector3 bottomLeftPos = new(thickness, y * spaceAndThickness, 0f);
            Vector3 topLeftPos = bottomLeftPos + toThicknessUp;
            Vector3 bottomRightPos = bottomLeftPos + toRowEnd;

            // Bottom-left Triangle //
            st.AddVertex(bottomLeftPos);
            st.AddVertex(topLeftPos);
            st.AddVertex(bottomRightPos);

            // Top-right Triangle //
            st.AddVertex(topLeftPos + toRowEnd);
            st.AddVertex(bottomRightPos);
            st.AddVertex(topLeftPos);
        }

        _arrays = st.CommitToArrays();
    }

    public override Godot.Collections.Array _CreateMeshArray() => _arrays;

    private void AddHeightEdge(SurfaceTool st, float spaceAndThickness, Vector3 toHeight, Vector3 toThickness, int startIdx)
    {
        Vector3 bottomLeftPos = Vector3.Right * (startIdx * spaceAndThickness);
        Vector3 topLeftPos = bottomLeftPos + toHeight;
        Vector3 bottomRightPos = bottomLeftPos + toThickness;

        // Bottom-left Triangle //
        st.AddVertex(bottomLeftPos);
        st.AddVertex(topLeftPos);
        st.AddVertex(bottomRightPos);

        // Top-right Triangle //
        st.AddVertex(topLeftPos + toThickness);
        st.AddVertex(bottomRightPos);
        st.AddVertex(topLeftPos);
    }
}
