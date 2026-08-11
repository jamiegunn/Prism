"""
Prism Workbench — Python helper for JupyterLite notebooks.

Provides convenient functions to interact with the Prism AI Research Workbench API
from within JupyterLite notebooks running in the browser.

Usage:
    import workbench

    # Chat with a model
    response = await workbench.chat("instance-id", "model-name", "Hello!")
    print(response)

    # Get experiment data
    experiment = await workbench.get_experiment("experiment-id")

    # Get dataset records
    records = await workbench.get_dataset("dataset-id")

    # Get logprobs for a prompt
    logprobs = await workbench.logprobs("instance-id", "model-name", "The cat sat")
"""

from pyodide.http import pyfetch  # type: ignore
import json

BASE_URL = "/api/v1"


async def _fetch(url: str, method: str = "GET", body: dict | None = None) -> dict:
    """Internal fetch helper using Pyodide's pyfetch."""
    options: dict = {"method": method}
    if body is not None:
        options["headers"] = {"Content-Type": "application/json"}
        options["body"] = json.dumps(body)

    response = await pyfetch(f"{BASE_URL}{url}", **options)

    if response.status >= 400:
        text = await response.string()
        raise Exception(f"API error {response.status}: {text}")

    if response.status == 204:
        return {}

    return await response.json()


async def chat(instance_id: str, model: str, prompt: str, **kwargs) -> str:
    """
    Send a chat message and return the assistant's response.

    Args:
        instance_id: The inference instance ID.
        model: The model name.
        prompt: The user message.
        **kwargs: Optional parameters (temperature, max_tokens, etc.)

    Returns:
        The assistant's response text.
    """
    body = {
        "instanceId": instance_id,
        "message": prompt,
        "systemPrompt": kwargs.get("system_prompt"),
        "temperature": kwargs.get("temperature"),
        "maxTokens": kwargs.get("max_tokens"),
        "logprobs": kwargs.get("logprobs", False),
    }
    # Remove None values
    body = {k: v for k, v in body.items() if v is not None}

    # Use the playground chat endpoint (non-streaming for simplicity)
    response = await pyfetch(
        f"{BASE_URL}/playground/chat",
        method="POST",
        headers={"Content-Type": "application/json"},
        body=json.dumps(body),
    )

    # Parse SSE response to extract the final content
    text = await response.string()
    content_parts = []
    for line in text.split("\n"):
        if line.startswith("data: "):
            try:
                data = json.loads(line[6:])
                if "content" in data:
                    content_parts.append(data["content"])
            except json.JSONDecodeError:
                pass

    return "".join(content_parts)


async def get_experiment(experiment_id: str) -> dict:
    """
    Get an experiment by ID.

    Args:
        experiment_id: The experiment GUID.

    Returns:
        The experiment data as a dictionary.
    """
    return await _fetch(f"/experiments/{experiment_id}")


async def get_dataset(dataset_id: str) -> dict:
    """
    Get a dataset by ID including its records.

    Args:
        dataset_id: The dataset GUID.

    Returns:
        The dataset data as a dictionary.
    """
    return await _fetch(f"/datasets/{dataset_id}")


async def get_dataset_records(dataset_id: str, page: int = 1, page_size: int = 100) -> dict:
    """
    Get records from a dataset with pagination.

    Args:
        dataset_id: The dataset GUID.
        page: Page number (1-based).
        page_size: Number of records per page.

    Returns:
        Paged result with items, totalCount, etc.
    """
    return await _fetch(f"/datasets/{dataset_id}/records?page={page}&pageSize={page_size}")


async def logprobs(instance_id: str, model: str, prompt: str, top_logprobs: int = 5) -> list:
    """
    Get logprobs for a prompt.

    Args:
        instance_id: The inference instance ID.
        model: The model name.
        prompt: The text to analyze.
        top_logprobs: Number of top alternatives per token.

    Returns:
        List of token logprob data.
    """
    body = {
        "instanceId": instance_id,
        "message": prompt,
        "logprobs": True,
        "topLogprobs": top_logprobs,
        "maxTokens": 1,
    }

    response = await pyfetch(
        f"{BASE_URL}/playground/chat",
        method="POST",
        headers={"Content-Type": "application/json"},
        body=json.dumps(body),
    )

    text = await response.string()
    logprob_data = []
    for line in text.split("\n"):
        if line.startswith("data: "):
            try:
                data = json.loads(line[6:])
                if "logprob" in data and data["logprob"] is not None:
                    logprob_data.append(data["logprob"])
            except json.JSONDecodeError:
                pass

    return logprob_data


async def list_models() -> list:
    """
    List all available inference instances/models.

    Returns:
        List of model instance data.
    """
    return await _fetch("/models/instances")


async def list_collections() -> list:
    """
    List all RAG collections.

    Returns:
        List of RAG collection data.
    """
    return await _fetch("/rag/collections")


async def rag_query(collection_id: str, query: str, top_k: int = 5, search_type: str = "Hybrid") -> list:
    """
    Search a RAG collection.

    Args:
        collection_id: The collection GUID.
        query: The search query.
        top_k: Number of results to return.
        search_type: "Vector", "Bm25", or "Hybrid".

    Returns:
        List of matching chunks with scores.
    """
    body = {
        "queryText": query,
        "topK": top_k,
        "searchType": search_type,
    }
    return await _fetch(f"/rag/collections/{collection_id}/query", method="POST", body=body)


def help():
    """Print available workbench functions."""
    print("""
Prism Workbench — Available Functions
=====================================

All functions are async — use 'await' to call them.

  await workbench.chat(instance_id, model, prompt, **kwargs)
      Chat with a model and get the response text.

  await workbench.logprobs(instance_id, model, prompt, top_logprobs=5)
      Get token logprobs for a prompt.

  await workbench.get_experiment(experiment_id)
      Get experiment data by ID.

  await workbench.get_dataset(dataset_id)
      Get dataset metadata by ID.

  await workbench.get_dataset_records(dataset_id, page=1, page_size=100)
      Get paginated dataset records.

  await workbench.list_models()
      List all inference instances.

  await workbench.list_collections()
      List all RAG collections.

  await workbench.rag_query(collection_id, query, top_k=5, search_type="Hybrid")
      Search a RAG collection.
""")
