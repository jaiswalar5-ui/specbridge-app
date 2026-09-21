document.addEventListener("DOMContentLoaded", () => {
    const generateBtn = document.getElementById("generateBtn");
    const rawRequirementInput = document.getElementById("rawRequirementInput");
    const outputSection = document.getElementById("outputSection");
    const specOutput = document.getElementById("specOutput");

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
                outputSection.style.display = "block";
                specOutput.textContent = JSON.stringify(data, null, 2);
            } catch (err) {
                console.error("Error generating spec:", err);
                alert("An error occurred while generating specification.");
            } finally {
                generateBtn.disabled = false;
                generateBtn.innerText = "Generate Specification";
            }
        });
    }
});
