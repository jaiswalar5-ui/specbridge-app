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

    const textElement = (tagName, className, text) => {
        const element = document.createElement(tagName);
        element.className = className;
        element.textContent = text ?? "";
        return element;
    };

    const renderAcceptanceCriteria = criteria => {
        if (!criteria?.length) return null;

        const details = document.createElement("details");
        details.className = "mt-2";
        const summary = textElement("summary", "fw-bold cursor-pointer text-primary", "Acceptance Criteria");
        const list = document.createElement("ul");
        list.className = "mt-2";
        criteria.forEach(criterion => list.append(textElement("li", "", criterion)));
        details.append(summary, list);
        return details;
    };

    const renderList = (elementId, items, renderItem) => {
        const element = document.getElementById(elementId);
        element.replaceChildren();
        if (!items?.length) {
            element.append(textElement("p", "text-muted small", "Nothing identified yet."));
            return;
        }

        const list = document.createElement("ul");
        list.className = "list-group list-group-flush";
        items.forEach(item => list.append(renderItem(item)));
        element.append(list);
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
                const metrics = [
                    [latestSpecification.functionalRequirements?.length || 0, "functional"],
                    [latestSpecification.userStories?.length || 0, "stories"],
                    [latestSpecification.clarifyingQuestions?.length || 0, "open gaps"],
                    [latestSpecification.risks?.length || 0, "risks"]
                ];
                const metricRow = document.getElementById("metricRow");
                metricRow.replaceChildren(...metrics.map(([count, label]) => {
                    const badge = textElement("span", "badge bg-success bg-opacity-25 text-success-emphasis p-2 me-2 mb-2 border border-success border-opacity-50", "");
                    badge.append(textElement("div", "fs-5 fw-bold", count), textElement("div", "small fw-normal text-uppercase", label));
                    return badge;
                }));

                renderList("functionalRequirements", latestSpecification.functionalRequirements, item => {
                    const entry = textElement("li", "list-group-item bg-transparent px-0 border-light py-2", "");
                    const heading = document.createElement("b");
                    heading.append(document.createTextNode(`${item.id} `), textElement("span", "pill text-muted text-uppercase", item.priority));
                    entry.append(heading, document.createTextNode(` ${item.description ?? ""}`));
                    const criteria = renderAcceptanceCriteria(item.acceptanceCriteria);
                    if (criteria) entry.append(criteria);
                    return entry;
                });
                renderList("userStories", latestSpecification.userStories, item => {
                    const entry = textElement("li", "list-group-item bg-transparent px-0 border-light py-2", "");
                    entry.append(textElement("b", "", item.id), document.createTextNode(` As a ${item.asA}, I want ${item.iWant}, so that ${item.soThat}.`));
                    const criteria = renderAcceptanceCriteria(item.acceptanceCriteria);
                    if (criteria) entry.append(criteria);
                    return entry;
                });
                renderList("nonFunctionalRequirements", latestSpecification.nonFunctionalRequirements, item => {
                    const entry = textElement("li", "list-group-item bg-transparent px-0 border-light py-2", "");
                    entry.append(textElement("b", "", item.category), document.createTextNode(` ${item.requirement ?? ""} `), textElement("span", "pill text-muted text-uppercase", `Target: ${item.target ?? ""}`));
                    return entry;
                });
                renderList("gapsAndRisks", [
                    ...(latestSpecification.clarifyingQuestions || []).map(item => ({ ...item, type: "Question", text: item.question, label: item.area, badgeClass: "badge bg-warning text-dark" })),
                    ...(latestSpecification.risks || []).map(item => ({ ...item, type: "Risk", text: item.description, label: item.severity, badgeClass: "badge bg-danger" }))
                ], item => {
                    const entry = textElement("li", "list-group-item bg-transparent px-0 border-light py-2", "");
                    const heading = document.createElement("b");
                    heading.append(textElement("span", item.badgeClass, item.type), document.createTextNode(` ${item.label ?? ""}`));
                    entry.append(heading, document.createTextNode(` ${item.text ?? ""}`));
                    return entry;
                });

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
