"""
visualize_embeddings.py

Connects directly to your pgvector-backed Postgres database, pulls product
embeddings, reduces them to 2D (or 3D) using PCA and/or UMAP, and renders
an interactive HTML scatter plot you can open in any browser.

Usage:
    pip install psycopg2-binary numpy pandas scikit-learn umap-learn plotly --break-system-packages
    python visualize_embeddings.py --conn "postgresql://postgres:postgres@103.
    165.162.229:5432/ecommerce"

Options:
    --table       Table name (default: products)
    --id-col      Primary key column (default: id)
    --label-col   Column to show on hover / use for point labels (default: name)
    --color-col   Column to color points by, e.g. category (default: category)
    --vector-col  Column holding the pgvector embedding (default: embedding)
    --method      pca | umap | tsne (default: umap if installed, else pca)
    --dims        2 or 3 (default: 2)
    --limit       Max rows to pull, useful for huge catalogs (default: 5000)
    --out         Output HTML file (default: embeddings.html)
"""

import argparse
import sys

import numpy as np
import pandas as pd


def parse_args():
    p = argparse.ArgumentParser(description="Visualize pgvector embeddings")
    p.add_argument("--conn", required=True, help="Postgres connection string")
    p.add_argument("--table", default="products")
    p.add_argument("--id-col", default="id")
    p.add_argument("--label-col", default="name")
    p.add_argument("--color-col", default="category")
    p.add_argument("--vector-col", default="embedding")
    p.add_argument("--method", default=None, choices=["pca", "umap", "tsne"])
    p.add_argument("--dims", type=int, default=2, choices=[2, 3])
    p.add_argument("--limit", type=int, default=5000)
    p.add_argument("--out", default="embeddings.html")
    return p.parse_args()


def fetch_data(conn_str, table, id_col, label_col, color_col, vector_col, limit):
    import psycopg2

    cols = f"{id_col}, {label_col}, {color_col}, {vector_col}"
    query = f"SELECT {cols} FROM {table} WHERE {vector_col} IS NOT NULL LIMIT %s;"

    with psycopg2.connect(conn_str) as conn:
        with conn.cursor() as cur:
            cur.execute(query, (limit,))
            rows = cur.fetchall()

    if not rows:
        print("No rows with embeddings found. Have you embedded any products yet?", file=sys.stderr)
        sys.exit(1)

    ids, labels, colors, raw_vectors = zip(*rows)

    # pgvector returns vectors as strings like "[0.01,-0.02,...]" via psycopg2's
    # default text format, so parse them into numpy arrays.
    def parse_vector(v):
        if isinstance(v, str):
            return np.array([float(x) for x in v.strip("[]").split(",")])
        return np.array(v)  # already a list/array-like (e.g. if using pgvector's psycopg adapter)

    vectors = np.vstack([parse_vector(v) for v in raw_vectors])

    return pd.DataFrame({"id": ids, "label": labels, "color": colors}), vectors


def reduce_dimensions(vectors, method, dims):
    n_samples = vectors.shape[0]

    if method is None:
        try:
            import umap  # noqa: F401
            method = "umap"
        except ImportError:
            method = "pca"

    if method == "umap":
        import umap
        n_neighbors = min(15, max(2, n_samples - 1))
        reducer = umap.UMAP(n_components=dims, n_neighbors=n_neighbors, random_state=42)
        return reducer.fit_transform(vectors), method

    if method == "tsne":
        from sklearn.manifold import TSNE
        perplexity = min(30, max(2, n_samples // 3))
        reducer = TSNE(n_components=dims, perplexity=perplexity, random_state=42)
        return reducer.fit_transform(vectors), method

    from sklearn.decomposition import PCA
    reducer = PCA(n_components=dims, random_state=42)
    return reducer.fit_transform(vectors), method


def plot(df, coords, dims, method, out_path):
    import plotly.express as px

    if dims == 2:
        df["x"], df["y"] = coords[:, 0], coords[:, 1]
        fig = px.scatter(
            df, x="x", y="y", color="color", hover_name="label",
            hover_data={"id": True, "x": False, "y": False},
            title=f"Product Embeddings ({method.upper()} projection, {len(df)} points)",
        )
    else:
        df["x"], df["y"], df["z"] = coords[:, 0], coords[:, 1], coords[:, 2]
        fig = px.scatter_3d(
            df, x="x", y="y", z="z", color="color", hover_name="label",
            hover_data={"id": True, "x": False, "y": False, "z": False},
            title=f"Product Embeddings ({method.upper()} projection, {len(df)} points)",
        )

    fig.update_traces(marker=dict(size=5, opacity=0.8))
    fig.write_html(out_path)
    print(f"Wrote {out_path} — open it in your browser.")


def main():
    args = parse_args()

    print(f"Connecting and fetching up to {args.limit} rows from '{args.table}'...")
    df, vectors = fetch_data(
        args.conn, args.table, args.id_col, args.label_col,
        args.color_col, args.vector_col, args.limit,
    )
    print(f"Fetched {len(df)} embeddings of dimension {vectors.shape[1]}.")

    print("Reducing dimensionality...")
    coords, method_used = reduce_dimensions(vectors, args.method, args.dims)

    print("Rendering plot...")
    plot(df, coords, args.dims, method_used, args.out)


if __name__ == "__main__":
    main()
