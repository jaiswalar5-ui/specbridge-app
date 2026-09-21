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
        element.innerHTML = items?.length ? items.map(renderItem).join("") : '<p class="spec-item">Nothing identified yet.</p>';
    };

    if (generateBtn) {
        generateBtn.addEventListener("click", async () => {
            const text = rawRequirementInput.value.trim();
            if (!text) {
                alert("Please enter requirements.");
                return;
            }

            generateBtn.disabled = true;
            generateBtn.innerText = "Analyzing...";

            try {
                const response = await fetch("/api/generate-spec", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({ prompt: text })
                });

                if (response.status === 429) {
                    alert("Rate limit exceeded: 10 requests per minute maximum. Please wait.");
                    return;
                }

                const data = await response.json();
                if (!response.ok) throw new Error(data.error || "Generation failed.");
                latestSpecification = data.specification;
                outputSection.hidden = false;
                document.getElementById("specTitle").textContent = latestSpecification.title;
                document.getElementById("specSummary").textContent = latestSpecification.summary;
                document.getElementById("metricRow").innerHTML = [
                    [latestSpecification.functionalRequirements?.length || 0, "functional"],
                    [latestSpecification.userStories?.length || 0, "stories"],
                    [latestSpecification.gaps?.length || 0, "open gaps"],
                    [latestSpecification.risks?.length || 0, "risks"]
                ].map(([count, label]) => `<span class="metric"><b>${count}</b>${label}</span>`).join("");
                renderList("functionalRequirements", latestSpecification.functionalRequirements, item => `<div class="spec-item"><b>${item.id} <span class="pill">${item.priority}</span></b>${item.description}</div>`);
                renderList("userStories", latestSpecification.userStories, item => `<div class="spec-item"><b>${item.id}</b>As a ${item.asA}, I want ${item.iWant}, so that ${item.soThat}.</div>`);
                renderList("nonFunctionalRequirements", latestSpecification.nonFunctionalRequirements, item => `<div class="spec-item"><b>${item.category}</b>${item.requirement}<br><span class="pill">Target: ${item.target}</span></div>`);
                renderList("gapsAndRisks", [...(latestSpecification.gaps || []).map(item => ({ ...item, type: "Gap", text: item.question })), ...(latestSpecification.risks || []).map(item => ({ ...item, type: "Risk", text: item.description }))], item => `<div class="spec-item"><b><span class="pill">${item.type}</span> ${item.area || item.severity}</b>${item.text}</div>`);
                requestStatus.textContent = "Specification ready.";
            } catch (err) {
                console.error("Error generating spec:", err);
                requestStatus.textContent = err.message;
            } finally {
                generateBtn.disabled = false;
                generateBtn.innerText = "Generate Specification";
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
