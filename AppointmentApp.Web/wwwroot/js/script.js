document.addEventListener("DOMContentLoaded", () => {
    const statusDiv = document.getElementById("status");
    const scheduleContainer = document.getElementById("schedule-container");
    const resetButton = document.getElementById("reset-button");
    let myClientId = null; 

    const wsProtocol = window.location.protocol === "https:" ? "wss:" : "ws:";
    const wsUrl = `${wsProtocol}//${window.location.host}/ws`;

    const socket = new WebSocket(wsUrl);

    socket.onopen = () => {
        console.log("WebSocket соединение установлено.");
        statusDiv.textContent = "Соединение установлено";
        statusDiv.className = "connected";
    };

    socket.onclose = () => {
        console.log("WebSocket соединение закрыто.");
        statusDiv.textContent = "Соединение потеряно. Попробуйте обновить страницу.";
        statusDiv.className = "disconnected";
    };

    socket.onerror = (error) => {
        console.error("WebSocket ошибка:", error);
        statusDiv.textContent = "Ошибка соединения.";
        statusDiv.className = "disconnected";
    };

    socket.onmessage = (event) => {
        const message = JSON.parse(event.data);
        console.log("Получено сообщение:", message);

        switch (message.Type) {
            case "initialState":
                renderSchedule(message.Payload);
                break;
            case "update":
                updateSlot(message.Payload);
                if (message.Payload.bookedByClientId && !myClientId) {
                    const slotElement = document.querySelector(`[data-doctor="${message.Payload.doctor}"][data-time="${message.Payload.time}"]`);
                    if (slotElement.classList.contains('my-booking')) {
                        myClientId = message.Payload.bookedByClientId;
                    }
                }
                break;
        }
    };

    function renderSchedule(schedule) {
        scheduleContainer.innerHTML = ""; 
        for (const doctor in schedule) {
            const doctorCard = document.createElement("div");
            doctorCard.className = "doctor-card";

            const doctorTitle = document.createElement("h2");
            doctorTitle.textContent = doctor;
            doctorCard.appendChild(doctorTitle);

            const slotsContainer = document.createElement("div");
            slotsContainer.className = "time-slots";

            schedule[doctor].forEach(slot => {
                const slotElement = document.createElement("div");
                slotElement.className = "slot";
                slotElement.textContent = slot.Time;
                slotElement.dataset.doctor = doctor; 
                slotElement.dataset.time = slot.Time;

                updateSlotElement(slotElement, slot.Status, slot.BookedByClientId);

                slotsContainer.appendChild(slotElement);
            });
            doctorCard.appendChild(slotsContainer);
            scheduleContainer.appendChild(doctorCard);
        }
    }

    function updateSlot(payload) {
        const { doctor, time, status, bookedByClientId } = payload;
        const slotElement = document.querySelector(`[data-doctor="${doctor}"][data-time="${time}"]`);
        if (slotElement) {
            updateSlotElement(slotElement, status, bookedByClientId);
        }
    }

    function updateSlotElement(element, status, bookedByClientId) {
        element.classList.remove("free", "booked", "my-booking");
        if (status === "Free") {
            element.classList.add("free");
        } else if (status === "Booked") {
            
            if (myClientId && myClientId === bookedByClientId) {
                element.classList.add("my-booking");
            } else {
                element.classList.add("booked");
            }
        }
    }

    scheduleContainer.addEventListener("click", (event) => {
        const target = event.target;
        if (target.classList.contains("slot") && target.classList.contains("free")) {
            const doctor = target.dataset.doctor;
            const time = target.dataset.time;

            const message = {
                type: "book",
                payload: {
                    doctor: doctor,
                    time: time
                }
            };
            socket.send(JSON.stringify(message));

            target.classList.remove('free');
            target.classList.add('my-booking');
            if (!myClientId) {
                myClientId = "temporary-id";
            }
        }
    });

    resetButton.addEventListener("click", () => {
        if (confirm("Вы уверены, что хотите сбросить всё расписание? Все брони будут отменены.")) {
            const message = {
                type: "reset" 
            };
            socket.send(JSON.stringify(message));
            console.log("Отправлен запрос на сброс расписания.");
        }
    });
});