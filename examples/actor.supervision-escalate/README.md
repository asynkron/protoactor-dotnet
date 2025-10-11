# EscalateSupervision

Example of actors escalating failures to their supervisors to trigger higher-
level handling. Child actors delegate unrecoverable errors up the hierarchy,
allowing supervisors to decide whether to restart or stop them.
