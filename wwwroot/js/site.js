document.addEventListener("DOMContentLoaded", () => {
    const generateBtn = document.getElementById("generateBtn");
    const rawRequirementInput = document.getElementById("rawRequirementInput");
    const outputSection = document.getElementById("outputSection");
    const specOutput = document.getElementById("specOutput");
    const characterCount = document.getElementById("characterCount");
    const requestStatus = document.getElementById("requestStatus");
    let latestSpecification;

    rawRequirementInput.addEventListener("input", () => {
        characterCount.textContent = `${rawRequirementInput.value.length.toLocaleString()} / 20,000`;
    });

    const renderList = (elementId, items, renderItem) => {
        const element = document.getElementById(elementId);
        element.innerHTML = items?.length ? `<ul class="list-group list-group-flush">${items.map(renderItem).join("")}</ul>` : '<p class="text-muted small">Nothing identified yet.</p>';
    };

    if (generateBtn) {
        generateBtn.addEventListener("click", async () => {
            const text = rawRequirementInput.value.trim();
            if (!text) {
                alert("Please enter requirements.");
                return;
            }

            const generateSpinner = document.getElementById("generateSpinner");
            const generateBtnText = document.getElementById("generateBtnText");

            generateBtn.disabled = true;
            if (generateSpinner) generateSpinner.classList.remove("d-none");
            if (generateBtnText) generateBtnText.innerText = "Analyzing...";

            // Progressive loading states messages
            const loadingMessages = [
                "Sending to AI...",
                "Analyzing requirements...",
                "Identifying gaps and risks...",
                "Structuring user stories...",
                "Finalizing specification...",
                "Almost there..."
            ];
            let messageIndex = 0;
            requestStatus.textContent = loadingMessages[messageIndex];
            requestStatus.classList.remove("text-danger");

            const statusInterval = setInterval(() => {
                messageIndex = Math.min(messageIndex + 1, loadingMessages.length - 1);
                requestStatus.textContent = loadingMessages[messageIndex];
            }, 3000);

            try {
                const response = await fetch("/api/generate-spec", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({ prompt: text })
                });

                clearInterval(statusInterval);

                if (response.status === 429) {
                    alert("Rate limit exceeded: 10 requests per minute maximum. Please wait.");
                    return;
                }

                const data = await response.json();
                if (!response.ok) throw new Error(data.error || "Generation failed.");
                latestSpecification = data.specification;

                // Show Output section
                outputSection.classList.remove('d-none');

                document.getElementById("specTitle").textContent = latestSpecification.title;
                document.getElementById("specSummary").textContent = latestSpecification.summary;
                document.getElementById("metricRow").innerHTML = [
                    [latestSpecification.functionalRequirements?.length || 0, "functional"],
                    [latestSpecification.userStories?.length || 0, "stories"],
                    [latestSpecification.clarifyingQuestions?.length || 0, "open gaps"],
                    [latestSpecification.risks?.length || 0, "risks"]
                ].map(([count, label]) => `<span class="badge bg-success bg-opacity-25 text-success-emphasis p-2 me-2 mb-2 border border-success border-opacity-50"><div class="fs-5 fw-bold">${count}</div><div class="small fw-normal text-uppercase">${label}</div></span>`).join("");

                renderList("functionalRequirements", latestSpecification.functionalRequirements, item => `<li class="list-group-item bg-transparent px-0 border-light py-2" style="font-size: 0.85rem;"><b>${item.id} <span class="pill text-muted text-uppercase" style="font-size: .65rem;">${item.priority}</span></b> ${item.description}</li>`);
                renderList("userStories", latestSpecification.userStories, item => `<li class="list-group-item bg-transparent px-0 border-light py-2" style="font-size: 0.85rem;"><b>${item.id}</b> As a ${item.asA}, I want ${item.iWant}, so that ${item.soThat}.</li>`);
                renderList("nonFunctionalRequirements", latestSpecification.nonFunctionalRequirements, item => `<li class="list-group-item bg-transparent px-0 border-light py-2" style="font-size: 0.85rem;"><b>${item.category}</b> ${item.requirement}<br><span class="pill text-muted text-uppercase" style="font-size: .65rem;">Target: ${item.target}</span></li>`);
                renderList("gapsAndRisks", [...(latestSpecification.clarifyingQuestions || []).map(item => ({ ...item, type: "Gap", text: item.question })), ...(latestSpecification.risks || []).map(item => ({ ...item, type: "Risk", text: item.description }))], item => `<li class="list-group-item bg-transparent px-0 border-light py-2 text-danger" style="font-size: 0.85rem;"><b><span class="pill text-danger text-uppercase" style="font-size: .65rem;">${item.type}</span> ${item.area || item.severity}</b> ${item.text}</li>`);

                requestStatus.textContent = "Specification ready.";
            } catch (err) {
                clearInterval(statusInterval);
                console.error("Error generating spec:", err);
                requestStatus.textContent = err.message;
                requestStatus.classList.add("text-danger");
            } finally {
                generateBtn.disabled = false;
                if (generateSpinner) generateSpinner.classList.add("d-none");
                if (generateBtnText) generateBtnText.innerText = "Generate Specification";
            }
        });
    }

    document.getElementById("copyBtn").addEventListener("click", async () => {
        if (!latestSpecification) return;
        await navigator.clipboard.writeText(JSON.stringify(latestSpecification, null, 2));
        requestStatus.textContent = "Specification JSON copied.";
    });

    document.getElementById("exportBtn").addEventListener("click", async () => {
        if (!latestSpecification) return;
        const response = await fetch("/api/export-pdf", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(latestSpecification)
        });
        if (!response.ok) {
            requestStatus.textContent = "PDF export failed.";
            return;
        }
        const link = document.createElement("a");
        link.href = URL.createObjectURL(await response.blob());
        link.download = "specbridge-specification.pdf";
        link.click();
        URL.revokeObjectURL(link.href);
        requestStatus.textContent = "PDF download started.";
    });
});
