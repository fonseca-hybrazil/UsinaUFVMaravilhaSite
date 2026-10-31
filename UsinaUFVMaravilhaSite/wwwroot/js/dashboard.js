(function () {
    "use strict";

    var POLL_MS = 3000;

    var numberFmt = new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

    function fmt(value) {
        if (value === null || value === undefined || Number.isNaN(value)) return "--";
        return numberFmt.format(value);
    }

    function setField(panel, field, text) {
        var el = panel.querySelector('[data-field="' + field + '"]');
        if (el) el.textContent = text;
    }

    function applyBreaker(panel, status) {
        var box = panel.querySelector('[data-field="breaker-box"]');
        var text = panel.querySelector('[data-field="breaker-text"]');
        box.classList.remove("fechado", "aberto");
        switch (status) {
            case "Fechado":
                box.classList.add("fechado");
                text.textContent = "FECH";
                break;
            case "Aberto":
                box.classList.add("aberto");
                text.textContent = "ABERTO";
                break;
            default:
                text.textContent = "--";
        }
    }

    function applyProtection(panel, field, active) {
        var el = panel.querySelector('[data-field="' + field + '"]');
        if (!el) return;
        el.classList.toggle("active", !!active);
    }

    function applyComms(panel, ok) {
        var dot = panel.querySelector('[data-field="comms-dot"]');
        var text = panel.querySelector('[data-field="comms-text"]');
        dot.classList.remove("ok", "alert");
        dot.classList.add(ok ? "ok" : "alert");
        text.textContent = ok ? "Relé OK" : "Relé sem comunicação";
    }

    function updateUfvPanel(id, ufv) {
        var panel = document.querySelector('.ufv-panel[data-ufv="' + id + '"]');
        if (!panel || !ufv) return;

        applyComms(panel, ufv.comunicacaoRele);
        applyBreaker(panel, ufv.disjuntor);

        var g = ufv.geracao || {};
        setField(panel, "power", fmt(g.activePowerKw));
        setField(panel, "pf", g.powerFactor != null ? g.powerFactor.toFixed(2) : "--");
        setField(panel, "reactive", fmt(g.reactivePowerKvar));
        setField(panel, "vab", fmt(g.vab));
        setField(panel, "vbc", fmt(g.vbc));
        setField(panel, "vca", fmt(g.vca));
        setField(panel, "ia", fmt(g.ia));
        setField(panel, "ib", fmt(g.ib));
        setField(panel, "ic", fmt(g.ic));
        setField(panel, "daily", fmt(g.dailyYieldMwh));
        setField(panel, "total", fmt(g.totalYieldMwh));

        var p = ufv.protecoes || {};
        applyProtection(panel, "prot-trip-t1", p.tripT1);
        applyProtection(panel, "prot-86-t2", p.bloqueio86T2);
        applyProtection(panel, "prot-59-1", p.sobretensao591);
        applyProtection(panel, "prot-59n1", p.sobretensaoNeutro59N1);
        applyProtection(panel, "prot-trafo-alarme", p.trafoAlarme);
        applyProtection(panel, "prot-trafo-trip", p.trafoTrip);
        applyProtection(panel, "prot-trafo-falha", p.trafoFalha);
    }

    function updateTopbar(state) {
        var dot = document.getElementById("conn-dot");
        var text = document.getElementById("conn-text");
        dot.classList.remove("ok", "alert");

        if (state.conectado) {
            dot.classList.add("ok");
            text.textContent = "Conectado";
        } else {
            dot.classList.add("alert");
            text.textContent = state.ultimoErro ? "Offline: " + state.ultimoErro : "Offline";
        }

        var ts = new Date(state.timestampUtc);
        document.getElementById("last-update").textContent = "Atualizado " + ts.toLocaleTimeString("pt-BR");
    }

    function updateParque(state) {
        document.getElementById("parque-potencia").textContent = fmt(state.potenciaAtivaTotalKw);
        document.getElementById("parque-diario").textContent = fmt(state.totalDiarioMwh);
        document.getElementById("parque-geral").textContent = fmt(state.totalGeralMwh);
    }

    function refresh() {
        fetch("/api/plant/state", { cache: "no-store" })
            .then(function (r) { return r.json(); })
            .then(function (state) {
                updateTopbar(state);
                updateParque(state);
                updateUfvPanel("ufv1", state.ufv1);
                updateUfvPanel("ufv2", state.ufv2);
            })
            .catch(function () {
                var dot = document.getElementById("conn-dot");
                var text = document.getElementById("conn-text");
                dot.classList.remove("ok");
                dot.classList.add("alert");
                text.textContent = "Falha ao consultar o servidor";
            });
    }

    function sendReset(target, button) {
        if (!window.confirm("Confirma o reset (" + target + ")?")) return;
        button.disabled = true;
        fetch("/api/plant/reset/" + target, { method: "POST" })
            .then(function (r) {
                if (!r.ok) throw new Error("Falha ao enviar reset");
            })
            .catch(function (err) {
                alert(err.message || "Falha ao enviar reset");
            })
            .finally(function () {
                setTimeout(function () { button.disabled = false; }, 1000);
            });
    }

    function sendBreakerCommand(command, label, button) {
        if (!window.confirm("ATENÇÃO: isso vai operar o disjuntor real.\n\nConfirma " + label + "?")) return;
        button.disabled = true;
        fetch("/api/plant/breaker/" + command, { method: "POST" })
            .then(function (r) {
                if (!r.ok) throw new Error("Falha ao enviar comando");
            })
            .catch(function (err) {
                alert(err.message || "Falha ao enviar comando");
            })
            .finally(function () {
                setTimeout(function () { button.disabled = false; }, 1000);
            });
    }

    document.addEventListener("click", function (ev) {
        var resetBtn = ev.target.closest("[data-reset]");
        if (resetBtn) {
            sendReset(resetBtn.getAttribute("data-reset"), resetBtn);
            return;
        }

        var breakerBtn = ev.target.closest("[data-breaker]");
        if (breakerBtn) {
            var acao = breakerBtn.classList.contains("btn-breaker-abrir") ? "ABRIR" : "FECHAR";
            var label = acao + " o disjuntor da " + breakerBtn.getAttribute("data-ufv-nome");
            sendBreakerCommand(breakerBtn.getAttribute("data-breaker"), label, breakerBtn);
        }
    });

    refresh();
    setInterval(refresh, POLL_MS);
})();
