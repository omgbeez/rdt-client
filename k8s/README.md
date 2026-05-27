# Kubernetes deployment

This directory contains a minimal Kubernetes deployment target for `rdt-client`.

Notes:

- Use the dedicated `Dockerfile.k8s` instead of the LinuxServer-based `Dockerfile`.
- Deploy a single replica only. The application uses SQLite and in-process background workers.
- Mount persistent storage at `/data`.
- The manifest disables file logging so logs go to stdout/stderr.

Build the image:

```bash
docker build -f Dockerfile.k8s -t rdt-client:k8s .
```

Apply the manifests:

```bash
kubectl apply -f k8s/rdt-client.yaml
```

You will still configure most runtime settings through the application itself, because they are stored in SQLite.
